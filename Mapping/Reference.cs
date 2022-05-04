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
    
    public static class Referencing
    {
        private static void Ref_Set(Program.Argument[] args)
        {
            Program.Argument key = Arguments.Read(in args, 1);
            Program.Argument pathTypeArg = Arguments.Read(in args, 2);
            Program.Argument path = Arguments.Read(in args, 3);

            PathResult pathType = Enum.Parse<PathResult>(pathTypeArg.Value, true);
            if (Map.References.ContainsKey(key.Value))
            {
                Reference toSet = Map.References[key.Value];

                if (pathType == PathResult.Folder && toSet.Contributors.Count > 0)
                {
                    const string msg = $"Cannot change PathType of a reference with consumers to folder. Remove consumer mappings first!";
                    Console.WriteLine(msg);
                    throw new ArgumentException(msg);
                }

                toSet.PathType = pathType;
                toSet.Path = path.Value;
                using (FileStream mapFile = File.Open(Map.MAPPINGS_FILE, FileMode.Open))
                {
                    XElement root = XElement.Load(mapFile);
                    XElement refElement = root.Elements("ref").First(e => e.Attribute("key")?.Value == key.Value);
                    refElement.SetAttributeValue("path", path.Value);
                    refElement.SetAttributeValue("pathType", pathType.ToString());
                    Map.SaveMap(mapFile, root);
                }
                Console.WriteLine($"Successfully set value of existing reference '{key.Value}'");
            }
            else
            {
                Reference toAdd = new(key.Value, path.Value, pathType);
                Map.References.Add(key.Value, toAdd);
                Map.Heads.Add(toAdd);
                using (FileStream mapFile = File.Open(Map.MAPPINGS_FILE, FileMode.Open))
                {
                    XElement root = XElement.Load(mapFile);
                    XElement referenceContainer = root.Element("references")!;
                    XElement refElement = new("ref", new XAttribute("key", key.Value), new XAttribute("path", path.Value), new XAttribute("pathType", pathType.ToString()));
                    referenceContainer.Add(refElement);
                    Map.SaveMap(mapFile, root);
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


            StringBuilder output = new(128);
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

            List<Mapping> toRemove = new(reference.Consumers.Count + reference.Contributors.Count);
            toRemove.AddRange(reference.Consumers);
            toRemove.AddRange(reference.Contributors);

            foreach (Mapping mapping in toRemove)
            {
                Reference.RemoveMapping(mapping);
            }

            Map.References.Remove(key);
            Map.Heads.Remove(reference);
            return true;
        }

        private static void Ref_Remove(Program.Argument[] args)
        {
            Program.Argument key = Arguments.Read(in args, 1);
            bool isKeyValid = Map.References.TryGetValue(key.Value, out Reference? reference);
            if (!isKeyValid)
            {
                Console.WriteLine($"Reference key '{key.Value}' does not exist.");
                return;
            }


            if (RefRemoveWithMappings(key.Value, reference!)) return;

            Map.References.Remove(key.Value);
            Map.Heads.Remove(reference!);
            using (FileStream mapFile = File.Open(Map.MAPPINGS_FILE, FileMode.Open))
            {
                XElement root = XElement.Load(mapFile);
                XElement refElement = root.Element("references")!.Elements("ref").First(e => e.Attribute("key")?.Value == key.Value);
                refElement.Remove();
                Map.SaveMap(mapFile, root);
            }
            Console.WriteLine($"Successfully removed '{key.Value}'");
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
                    string msg = $"Unexpected token {args[i].Value}.";
                    Console.WriteLine(msg);
                    throw new ArgumentException(msg);
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
                        string msg = $"Unknown argument '{args[i].Value}'.";
                        Console.WriteLine(msg);
                        throw new ArgumentException(msg);
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

            IEnumerable<Reference> references = Map.References.Values;
            if (keySearch != null)
            {
                references = references.Where(r => r.Key.Contains(keySearch));
            }

            StringBuilder output = new(128);

            output.AppendLine($"References (Total {references.Count()}):");

            foreach (Reference reference in references)
            {
                output.Append($"{reference.Key}");
                if (options.HasFlag(RefListOptions.ShowPath)) output.Append($": {reference.Path} ({reference.PathType.ToString()})");
                output.Append('\n');
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
            switch (args[0].Value.ToLower())
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
                default:
                    string msg = $"Unknown argument '{args[0].Value}'.";
                    Console.WriteLine(msg);
                    throw new ArgumentException(msg);
            }
        }
    }
}
