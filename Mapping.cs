using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HTMLBuilder
{
    public enum PathResult
    { 
        File,
        Folder
    }
    public enum Insertion
    {
        Contents,
        Self
    }
    public class Reference
    {
        public string Key { get; set; }
        public string Path { get; set; }
        public PathResult PathType { get; set; }

        public Reference(string key, string path, PathResult pathType)
        {
            Key = key;
            Path = path;
            PathType = pathType;
        }

        public List<Mapping> Consumers = new();
        public List<Mapping> Contributors = new();

        public HashSet<Reference> RecursiveDependencies { get; private set; } = new();

        /// <summary>
        /// Travel up the tree and call the callback for each reference.
        /// </summary>
        private void IterateConsumers(Action<Reference> callback)
        {
            HashSet<Reference> added = new();
            Queue<Reference> toExplore = new();
            Queue<Reference> toIterate = new();

            toExplore.Enqueue(this);
            added.Add(this);

            // Discovery
            while (toExplore.Count > 0)
            {
                Reference current = toExplore.Dequeue();
                foreach (Mapping consumer in current.Consumers)
                {
                    Reference consumerRef = consumer.Consumer;
                    if (added.Contains(consumerRef)) continue;
                    
                    toExplore.Enqueue(consumerRef);
                    toIterate.Enqueue(consumerRef);
                    added.Add(consumerRef);
                }
            }

            // Iteration
            while (toIterate.Count > 0)
            {
                Reference current = toIterate.Dequeue();
                callback(current);
            }
        }

        private void AddDependency(Reference dependency)
        {
            RecursiveDependencies.Add(dependency);
            IterateConsumers((dep) => dep.RecursiveDependencies.Add(dependency));
        }

        private void RemoveDependency(Reference dependency)
        {
            RecursiveDependencies.Remove(dependency);
            IterateConsumers((dep) => dep.RecursiveDependencies.Remove(dependency));
        }

        public static bool AddMapping(Mapping mapping)
        {
            if (mapping.Contributor.RecursiveDependencies.Contains(mapping.Consumer))
            {
                Console.WriteLine($"ERROR: Reference {mapping.Contributor.Key} is already dependent on {mapping.Consumer.Key}. Circular dependencies are not allowed.");
                return false;
            }

            mapping.Contributor.Consumers.Add(mapping);
            mapping.Consumer.Contributors.Add(mapping);
            mapping.Consumer.AddDependency(mapping.Contributor);
            return true;
        }

        public static void RemoveMapping(Mapping mapping)
        {
            mapping.Contributor.Consumers.Remove(mapping);
            mapping.Consumer.Contributors.Remove(mapping);
            mapping.Consumer.RemoveDependency(mapping.Contributor);
        }
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
        const string MAP_FILE = "HBMappings.xml";

        public static Dictionary<string, Reference> References { get; private set; } = new();
        public static HashSet<Reference> Heads { get; private set; } = new();

        static void SaveMap(FileStream stream, XElement root)
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

        private static void Ref_Set(Program.Argument[] args)
        {
            Program.Argument key = Arguments.Read(in args, 1);
            Program.Argument pathTypeArg = Arguments.Read(in args, 2);
            Program.Argument path = Arguments.Read(in args, 3);

            PathResult pathType = Enum.Parse<PathResult>(pathTypeArg.Value, true);
            if (References.ContainsKey(key.Value))
            {
                Reference toSet = References[key.Value];

                if (pathType == PathResult.Folder && toSet.Contributors.Count > 0)
                {
                    Console.WriteLine($"Cannot change PathType of a reference with consumers to folder. Remove consumer mappings first!");
                    throw new ArgumentException();
                }

                toSet.PathType = pathType;
                toSet.Path = path.Value;
                using (FileStream mapFile = File.Open(MAP_FILE, FileMode.Open))
                {
                    XElement root = XElement.Load(mapFile);
                    XElement refElement = root.Elements("ref").First(e => e.Attribute("key")?.Value == key.Value);
                    refElement.SetAttributeValue("path", path.Value);
                    refElement.SetAttributeValue("pathType", pathType.ToString());
                    SaveMap(mapFile, root);
                }
                Console.WriteLine($"Successfully set value of existing reference '{key.Value}'");
            }
            else
            {
                Reference toAdd = new Reference(key.Value, path.Value, pathType);
                References.Add(key.Value, toAdd);
                Heads.Add(toAdd);
                using (FileStream mapFile = File.Open(MAP_FILE, FileMode.Open))
                {
                    XElement root = XElement.Load(mapFile);
                    XElement referenceContainer = root.Element("references")!;
                    XElement refElement = new XElement("ref", new XAttribute("key", key.Value), new XAttribute("path", path.Value), new XAttribute("pathType", pathType.ToString()));
                    referenceContainer.Add(refElement);
                    SaveMap(mapFile, root);
                }
                Console.WriteLine($"Successfully added reference '{key.Value}'.");
            }
        }

        /// <summary>
        /// Handle removal of a reference with attached mappings. Returns true if the operation was performed or cancelled.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        private static bool RefRemoveWithMappings(string key, Reference reference)
        {
            bool hasConsumers = reference!.Consumers.Count > 0;
            bool hasContributors = reference!.Contributors.Count > 0;
            if (!hasConsumers && !hasContributors) return false;
            
            
            StringBuilder output = new StringBuilder(128);
            output.AppendLine($"Reference key '{key}' is still dependent on other references:");

            if (hasConsumers) output.AppendLine("Consumers:");
            foreach (Mapping consumer in reference.Consumers)
            {
                output.AppendLine($"\t{consumer.Consumer.Key}");
            }

            if (hasContributors) output.AppendLine("Contributors:");
            foreach (Mapping contributor in reference.Contributors)
            {
                output.AppendLine($"\t{contributor.Contributor.Key}");
            }

            output.AppendLine($"Are you sure you want to remove the reference and all its mappings? y/n");
            Console.WriteLine(output);

            while (true)
            {
                string? input = Console.ReadLine();
                if (input == "y") break;
                if (input == "n") return true;
            }

            List<Mapping> toRemove = new List<Mapping>(reference.Consumers.Count + reference.Contributors.Count);
            toRemove.AddRange(reference.Consumers);
            toRemove.AddRange(reference.Contributors);

            foreach (Mapping mapping in toRemove)
            {
                Reference.RemoveMapping(mapping);
            }

            References.Remove(key);
            Heads.Remove(reference);
            return true;
        }

        private static void Ref_Remove(Program.Argument[] args)
        {
            Program.Argument key = Arguments.Read(in args, 1);
            bool isKeyValid = References.TryGetValue(key.Value, out Reference? reference);
            if (!isKeyValid)
            {
                Console.WriteLine($"Reference key '{key}' does not exist.");
                return;
            }


            if (RefRemoveWithMappings(key.Value, reference!)) return;

            References.Remove(key.Value);
            Heads.Remove(reference!);
        }

        [Flags]
        private enum RefListOptions
        {
            ShowPath = 1 << 0,
            ShowMappings = 1 << 1
        }
        private static RefListOptions RefListGetOptions(Program.Argument[] args, int startIndex)
        {
            RefListOptions options = 0;
            for (int i = startIndex; i < args.Length; i++)
            {
                if (!args[i].IsOption)
                {
                    Console.WriteLine($"Unexpected token {args[i].Value}.");
                    throw new ArgumentException();
                }
                switch (args[i].Value)
                {
                    case "p":
                    case "path":
                        options |= RefListOptions.ShowPath;
                        break;
                    case "m":
                    case "mappings":
                        options |= RefListOptions.ShowMappings;
                        break;
                    default:
                        Console.WriteLine($"Unknown argument '{args[i].Value}'.");
                        throw new ArgumentException();
                }
            }
            return options;
        }
        private static void Ref_List(Program.Argument[] args)
        {
            int optionStartIndex = 1;
            string? keySearch = null;
            if (args.Length > 1 && !args[1].IsOption)
            {
                optionStartIndex++;
                keySearch = args[1].Value;
            }
            RefListOptions options = RefListGetOptions(args, optionStartIndex);

            IEnumerable<Reference> references = References.Values;
            if (keySearch != null)
            {
                references = references.Where(r => r.Key.Contains(keySearch));
            }

            StringBuilder output = new StringBuilder(128);

            output.AppendLine($"References (Total {references.Count()}):");

            foreach (Reference reference in references)
            {
                output.Append($"{reference.Key}");
                if (options.HasFlag(RefListOptions.ShowPath)) output.Append($": {reference.Path} ({reference.PathType.ToString()})");
                output.Append("\n");
                if (options.HasFlag(RefListOptions.ShowMappings))
                {
                    foreach (Mapping mapping in reference.Consumers)
                    {
                        output.AppendLine($"\tthis <- {mapping.Contributor.Key}");
                    }
                    foreach (Mapping mapping in reference.Contributors)
                    {
                        output.AppendLine($"\tthis -> {mapping.Contributor.Key}");
                    }
                }
            }
            Console.WriteLine(output);

        }

        public static void Command_Ref(Program.Argument[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Too few arguments");
                return;
            }
            switch(args[0].Value.ToLower())
            {
                case "set":
                    Ref_Set(args);
                    break;
                case "remove":
                    Ref_Remove(args);
                    break;
                case "list":
                    Ref_List(args);
                    break;
            }
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
            bool wasCreated = !File.Exists(MAP_FILE);
            using (FileStream mapFile = File.Open(MAP_FILE, FileMode.OpenOrCreate))
            {
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
    

}
