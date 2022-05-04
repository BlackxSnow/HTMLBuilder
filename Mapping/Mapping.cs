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
        public List<SearchParam> ConsumerSearch ;
        public List<SearchParam> ContributorSearch;

        public Mapping(string key, Reference consumer, Reference contributor, List<SearchParam> consumerSearch, List<SearchParam> contributorSearch)
        {
            Key = key;
            Consumer = consumer;
            Contributor = contributor;
            ConsumerSearch = consumerSearch;
            ContributorSearch = contributorSearch;
        }
    }

    public static class Map
    {
        public const string MAPPINGS_FILE = "HBMappings.xml";

        public static Dictionary<string, Reference> References { get; private set; } = new();
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
        public static void Command_Map(Program.Argument[] args)
        {
            
        }

        public static bool AddMapping(Mapping mapping)
        {
            bool wasSuccessful = Reference.AddMapping(mapping);
            if (!wasSuccessful) return false;

            Heads.Remove(mapping.Contributor);
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

                References.Add(key!, new Reference(key!, path!, pathType));
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
            foreach (XElement mappingData in mappings.Elements("mapping"))
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
                XElement? contributorSearch = mappingData.Element("contributorSearch");
                NullCheck(contributorSearch, $"Mapping '{key}' did not have a contributor search.");

                Reference.AddMapping(new Mapping(key!, consumer!, contributor!, LoadSearchParams(consumerSearch!, key!), LoadSearchParams(contributorSearch!, key!)));
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
