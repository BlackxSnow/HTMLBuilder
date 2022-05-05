using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HTMLBuilder
{
    public enum Insertion
    {
        Contents,
        Self
    }

    public class SearchParam
    {
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }

        public SearchParam(string attributeName, string attributeValue)
        {
            AttributeName = attributeName;
            AttributeValue = attributeValue;
        }
    }

    public class Mapping
    {
        public string Key { get; set; }
        public Reference Consumer;
        public Reference Contributor;
        public List<SearchParam> ConsumerSearch;
        public string? ConsumerNameSearch;
        public List<SearchParam> ContributorSearch;
        public string? ContributorNameSearch;

        public Mapping(string key, Reference consumer, Reference contributor, List<SearchParam> consumerSearch, string? consumerNameSearch, List<SearchParam> contributorSearch, string? contributorNameSearch)
        {
            Key = key;
            Consumer = consumer;
            Contributor = contributor;
            ConsumerSearch = consumerSearch;
            ConsumerNameSearch = consumerNameSearch;
            ContributorSearch = contributorSearch;
            ContributorNameSearch = contributorNameSearch;
        }
    }

    public static class Mapper
    {
        public const string MAPPINGS_FILE = "HBMappings.xml";

        public static Dictionary<string, Reference> References { get; private set; } = new();
        public static Dictionary<string, Mapping> Mappings { get; private set; } = new();
        public static HashSet<Reference> Heads { get; private set; } = new();

        public static void SaveMap(FileStream stream, XElement root)
        {
            stream.SetLength(0);
            root.Save(stream);
            stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);
        }

        // ref [Set/Remove/List] [key] [file/folder] [path]
        // map [Set/Remove/List] [key] [ConsumerKey] [ContributorKey] --contributor [searchParams] --consumer [searchParams]
        // map list [?search] [options (-v --verbose)]

        private static List<SearchParam> ParseSearch(Arguments.Argument[] args, int startIndex, out int endIndex, out string? elementNameSearch)
        {
            List<SearchParam> result = new();
            elementNameSearch = null;
            while (args.Length > startIndex && !args[startIndex].IsOption)
            {
                string[] parts = args[startIndex].Value.Split('=');
                if (parts.Length != 2)
                {
                    throw new ArgumentException($"Invalid search parameter: '{args[startIndex].Value}'. Expected 'key=value' format.");
                }
                if (parts[0].ToLower() == "name")
                {
                    elementNameSearch = parts[1];
                    startIndex++;
                    continue;
                }
                result.Add(new SearchParam(parts[0], parts[1]));
                startIndex++;
            }
            endIndex = startIndex;
            return result;
        }

        private static void SearchOptionsToXML(ref XElement searchRoot, List<SearchParam> searchParams)
        {
            foreach (SearchParam searchParam in searchParams)
            {
                XElement searchElement = new XElement("attributeSearchParam", new XAttribute("name", searchParam.AttributeName), new XAttribute("value", searchParam.AttributeValue));
                searchRoot.Add(searchElement);
            }
        }

        private static void Map_Set(Arguments.Argument[] args)
        {
            Arguments.Argument key = Arguments.Read(args, 1);
            Arguments.Argument consumerKey = Arguments.Read(args, 2);
            Arguments.Argument contributorKey = Arguments.Read(args, 3);
            bool isConsumerValid = References.TryGetValue(consumerKey.Value, out Reference? consumer);
            bool isContributorValid = References.TryGetValue(contributorKey.Value, out Reference? contributor);

            if (!isConsumerValid)
            {
                string msg = $"Consumer reference '{consumerKey.Value}' does not exist.";
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }
            if (!isContributorValid)
            {
                string msg = $"Contributor reference '{contributorKey.Value}' does not exist.";
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }
            Arguments.Argument firstSearch = Arguments.Read(args, 4);
            if (!firstSearch.IsOption)
            {
                throw new ArgumentException($"Unexpected token '{firstSearch.Value}'. Expected search option.");
            }
            Mapping mapping;
            if (firstSearch.Value.ToLower() == "contributor")
            {
                List<SearchParam> contributorSearch = ParseSearch(args, 5, out int endIndex, out string? contributorNameSearch);
                Arguments.Argument secondSearch = Arguments.Read(args, endIndex++);
                if (secondSearch.Value.ToLower() != "consumer")
                {
                    throw new ArgumentException($"Unexpected token '{secondSearch.Value}'. Expected search option.");
                }
                List<SearchParam> consumerSearch = ParseSearch(args, endIndex, out endIndex, out string? consumerNameSearch);
                mapping = new Mapping(key.Value, consumer!, contributor!, consumerSearch, consumerNameSearch, contributorSearch, contributorNameSearch);
            }
            else if (firstSearch.Value.ToLower() == "consumer")
            {
                List<SearchParam> consumerSearch = ParseSearch(args, 5, out int endIndex, out string? consumerNameSearch);
                Arguments.Argument secondSearch = Arguments.Read(args, endIndex++);
                if (secondSearch.Value.ToLower() != "contributor")
                {
                    throw new ArgumentException($"Unexpected token '{secondSearch.Value}'. Expected search option.");
                }
                List<SearchParam> contributorSearch = ParseSearch(args, endIndex, out endIndex, out string? contributorNameSearch);
                mapping = new Mapping(key.Value, consumer!, contributor!, consumerSearch, consumerNameSearch, contributorSearch, contributorNameSearch);
            }
            else
            {
                throw new ArgumentException($"Unexpected token '{firstSearch.Value}'. Expected search option.");
            }

            if (Mappings.TryGetValue(key.Value, out Mapping? existing))
            {
                Reference.RemoveMapping(existing);
            }

            Reference.AddMapping(mapping);

            using FileStream mapFile = File.Open(MAPPINGS_FILE, FileMode.Open);
            XElement root = XElement.Load(mapFile);
            XElement mappingElement = root.Element("mappings")!;
            XElement map = new XElement("map", new XAttribute("key", key.Value), new XAttribute("consumer", consumerKey.Value), new XAttribute("contributor", contributorKey.Value));
            mappingElement.Add(map);
            XElement consumerSearchElement = new XElement("consumerSearch");
            if (mapping.ConsumerNameSearch != null) consumerSearchElement.SetAttributeValue("name", mapping.ConsumerNameSearch);
            XElement contributorSearchElement = new XElement("contributorSearch");
            if (mapping.ContributorNameSearch != null) contributorSearchElement.SetAttributeValue("name", mapping.ContributorNameSearch);

            map.Add(consumerSearchElement);
            map.Add(contributorSearchElement);

            SearchOptionsToXML(ref consumerSearchElement, mapping.ConsumerSearch);
            SearchOptionsToXML(ref contributorSearchElement, mapping.ContributorSearch);

            SaveMap(mapFile, root);

            Console.WriteLine($"Successfully created mapping '{key.Value}'.");
        }

        
        private static void Map_Remove(Arguments.Argument[] args)
        {
            string key = Arguments.Read(in args, 1).Value;
            if (!Mappings.ContainsKey(key))
            {
                string msg = $"No mapping '{key}' exists.";
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }
            Mapping mapping = Mappings[key];
            string consumerKey = mapping.Consumer.Key;
            string contributorKey = mapping.Contributor.Key;
            Reference.RemoveMapping(Mappings[key]);

            using FileStream mapFile = File.Open(MAPPINGS_FILE, FileMode.Open);
            XElement root = XElement.Load(mapFile);
            XElement mappingElement = root.Element("mappings")!;
            mappingElement.Elements("map").Where(x => x.Attribute("key")!.Value == key).Remove();
            SaveMap(mapFile, root);

            Console.WriteLine($"Successfully removed mapping {key}. (Consumer: {consumerKey}, Contributor: {contributorKey})");
        }

        [Flags]
        private enum MapListOptions
        {
            Verbose = 1
        }
        private static void Map_List(Arguments.Argument[] args)
        {
            int optionsStartIndex = 1;
            string? searchKey = null;
            if (args.Length > 1 && !args[1].IsOption)
            {
                optionsStartIndex++;
                searchKey = args[1].Value;
            }
            MapListOptions options = args.Length > optionsStartIndex && (args[optionsStartIndex].Value == "v" || args[optionsStartIndex].Value == "verbose") ? MapListOptions.Verbose : 0;
            IEnumerable<Reference> references = Mapper.References.Values;

            StringBuilder output = new(128);
            output.AppendLine($"Mappings:");
            foreach(Reference reference in references)
            {
                foreach(Mapping mapping in reference.Contributors)
                {
                    if (searchKey != null && mapping.Key.Contains(searchKey)) continue;

                    output.Append($"{mapping.Key}: {mapping.Consumer.Key} <- {mapping.Contributor.Key}");

                    if (options.HasFlag(MapListOptions.Verbose))
                    {
                        output.Append($" - (");
                        if (mapping.ConsumerNameSearch != null) output.Append($" name={mapping.ConsumerNameSearch}");
                        foreach (SearchParam param in mapping.ConsumerSearch)
                        {
                            output.Append($" {param.AttributeName}={param.AttributeValue}");
                        }
                        output.Append($" ) <- (");
                        if (mapping.ContributorNameSearch != null) output.Append($" name={mapping.ContributorNameSearch}");
                        foreach (SearchParam param in mapping.ContributorSearch)
                        {
                            output.Append($" {param.AttributeName}={param.AttributeValue}");
                        }
                        output.Append($" )");
                    }
                    output.Append('\n');
                }
            }
            Console.WriteLine(output);
        }

        public static void Command_Map(Arguments.Argument[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Too few arguments");
                return;
            }
            switch (args[0].Value.ToLower())
            {
                case "set":
                    Map_Set(args);
                    break;
                case "remove":
                    Map_Remove(args);
                    break;
                case "list":
                    Map_List(args);
                    break;
                default:
                    string msg = $"Unknown argument '{args[0].Value}'.";
                    Console.WriteLine(msg);
                    throw new ArgumentException(msg);
            }
        }

        public static bool AddMapping(Mapping mapping)
        {
            bool wasSuccessful = Reference.AddMapping(mapping);
            if (!wasSuccessful) return false;

            return true;
        }

        private static void NullCheck(object? value, string message)
        {
            if (value == null)
            {
                throw new InvalidDataException(message);
            }
        }

        private static void LoadReferences(XElement references)
        {
            uint count = 0;
            string lastKey = "<references> tag";
            foreach (XElement refData in references.Elements("ref"))
            {
                string? key = refData.Attribute("key")?.Value;
                NullCheck(key, $"Reference entry after '{lastKey}' did not have a key.");
                string? path = refData.Attribute("path")?.Value;
                NullCheck(path, $"Reference '{key}' did not have a path.");
                string? pathTypeString = refData.Attribute("pathType")?.Value;
                NullCheck(pathTypeString, $"Reference '{key}' did not have a path type (file or folder).");
                PathResult pathType = (PathResult)Enum.Parse(typeof(PathResult), pathTypeString!, true);

                Reference reference = new Reference(key!, path!, pathType);
                References.Add(key!, reference);
                Heads.Add(reference);
                lastKey = key!;
                count++;
            }
            Console.WriteLine($"Loaded {count} references.");
        }

        private static List<SearchParam> LoadSearchParams(XElement container, string parentKey)
        {
            List<SearchParam> results = new();
            foreach (XElement searchParam in container.Elements("attributeSearchParam"))
            {
                string? attributeName = searchParam.Attribute("name")?.Value;
                NullCheck(attributeName, $"SearchParam of '{parentKey}' did not have an attribute name.");
                string? attributeValue = searchParam.Attribute("value")?.Value;
                NullCheck(attributeValue, $"SearchParam of '{parentKey}' did not have an attribute value.");

                results.Add(new SearchParam(attributeName!, attributeValue!));
            }
            return results;
        }

        private static void LoadMappings(XElement mappings)
        {
            uint count = 0;
            string lastKey = "<mappings> tag";
            foreach (XElement mappingData in mappings.Elements("map"))
            {
                string? key = mappingData.Attribute("key")?.Value;
                NullCheck(key, $"Mapping entry after '{lastKey}' did not have a key.");
                string? consumerKey = mappingData.Attribute("consumer")?.Value;
                NullCheck(consumerKey, $"Mapping '{key}' did not have a consumer.");
                string? contributorKey = mappingData.Attribute("contributor")?.Value;
                NullCheck(contributorKey, $"Mapping '{key}' did not have a contributor.");

                bool isConsumerValid = References.TryGetValue(consumerKey!, out Reference? consumer);
                bool isContributorValid = References.TryGetValue(contributorKey!, out Reference? contributor);

                if (!isConsumerValid)
                {
                    Console.WriteLine($"ERROR: Mapping '{key}' has an invalid consumer '{consumerKey}'.");
                    throw new InvalidDataException();
                }
                if (!isContributorValid)
                {
                    Console.WriteLine($"ERROR: Mapping '{key}' has an invalid contributor '{contributorKey}'.");
                    throw new InvalidDataException();
                }

                XElement? consumerSearch = mappingData.Element("consumerSearch");
                NullCheck(consumerSearch, $"Mapping '{key}' did not have a consumer search.");
                string? consumerNameSearch = consumerSearch!.Attribute("name")?.Value;
                XElement? contributorSearch = mappingData.Element("contributorSearch");
                string? contributorNameSearch = contributorSearch!.Attribute("name")?.Value;
                NullCheck(contributorSearch, $"Mapping '{key}' did not have a contributor search.");

                Reference.AddMapping(new Mapping(key!, consumer!, contributor!, LoadSearchParams(consumerSearch!, key!), consumerNameSearch, LoadSearchParams(contributorSearch!, key!), contributorNameSearch));
                lastKey = key!;
                count++;
            }
            Console.WriteLine($"Loaded {count} mappings.");
        }

        public static void Initialize()
        {
            bool wasCreated = !File.Exists(MAPPINGS_FILE);
            using FileStream mapFile = File.Open(MAPPINGS_FILE, FileMode.OpenOrCreate);
            XElement root;
            if (wasCreated)
            {
                Console.WriteLine($"Mappings file does not exist, creating one...");
                root = new XElement("data", null!);
                root.SetElementValue("references", "");
                root.SetElementValue("mappings", "");
                root.Save(mapFile);
            }
            else
            {
                root = XElement.Load(mapFile);
            }
            XElement? references = root.Element("references");
            NullCheck(references, $"Mapping file did not have a references element.");
            XElement? mappings = root.Element("mappings");
            NullCheck(mappings, $"Mapping file did not have a mappings element.");

            Console.WriteLine($"Loading references...");
            LoadReferences(references!);
            Console.WriteLine($"Loading mappings...");
            LoadMappings(mappings!);
            Console.WriteLine($"Successfully loaded all mappings data.");
        }
    }
    

}
