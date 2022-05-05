using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Security.Cryptography;

namespace HTMLBuilder
{
    public class BuilderException : Exception
    {
        public BuilderException(string message) : base(message)
        {
        }
    }

    public static class Builder
    {      
        private static Dictionary<Reference, List<HtmlDocument>> _CompletedReferences = new();

        private static IEnumerable<HtmlNode> GetElementFromSearch(HtmlDocument document, List<SearchParam> search, string? name)
        {
            IEnumerable<HtmlNode> query = from node in (name == null ? document.DocumentNode.Descendants() : document.DocumentNode.Descendants(name)) select node;
            
            foreach (SearchParam param in search)
            {
                query = from node in query where node.Attributes.Contains(param.AttributeName) && node.Attributes[param.AttributeName].Value == param.AttributeValue select node;
            }
            return query;
        }

        private static List<HtmlDocument> BuildFolder(Reference reference)
        {
            if (!Directory.Exists(reference.Path))
            {
                throw new BuilderException($"Error while loading reference '{reference.Key}': Directory '{reference.Path}' does not exist.");
            }
            IEnumerable<string> files = from file in Directory.GetFiles(reference.Path) where file.EndsWith(".html") select file;
            if (!files.Any())
            {
                Console.WriteLine($"Warning while loading reference '{reference.Key}': Directory '{reference.Path}' is empty.");
            }
            List<HtmlDocument> loaded = new();

            Console.WriteLine($"Loading reference '{reference.Key}' (folder - {files.Count()} files)...");

            foreach (string file in files)
            {
                HtmlDocument document = new HtmlDocument();
                document.Load(file);
                loaded.Add(document);
                Console.WriteLine($"\tLoaded file '{Path.GetFileName(file)}'");
            }

            _CompletedReferences.Add(reference, loaded);
            Console.WriteLine($"Successfully loaded {loaded.Count} files from reference '{reference.Key}'");
            return loaded;
        }            

        private static HtmlDocument BuildFile(Reference reference)
        {
            if (!File.Exists(reference.Path))
            {
                throw new BuilderException($"Error while building reference '{reference.Key}': File '{reference.Path}' does not exist.");
            }
            HtmlDocument result = new HtmlDocument();
            result.Load(reference.Path);
            
            foreach (Mapping mapping in reference.Contributors)
            {
                List<HtmlDocument> contributors = _CompletedReferences[mapping.Contributor];
                IEnumerable<HtmlNode> insertionNode = GetElementFromSearch(result, mapping.ConsumerSearch, mapping.ConsumerNameSearch);
                if (insertionNode.Count() != 1) throw new BuilderException($"Error while building reference '{reference.Key}': Expected single insertion node for contributor '{mapping.Contributor.Key}', found {insertionNode.Count()}.");

                foreach (HtmlDocument contributor in contributors)
                {
                    IEnumerable<HtmlNode> extractionNode = GetElementFromSearch(contributor, mapping.ContributorSearch, mapping.ContributorNameSearch);
                    if (extractionNode.Count() != 1) throw new BuilderException($"Error while building reference '{reference.Key}': Expected single extraction node for contributor '{mapping.Contributor.Key}', found {extractionNode.Count()}.");
                    if (mapping.Flags.HasFlag(MappingOptions.Unpack))
                    {
                        insertionNode.First().AppendChildren(extractionNode.First().ChildNodes);
                    }
                    else
                    {
                        insertionNode.First().AppendChild(extractionNode.First());
                    }
                    
                }
            }
            List<HtmlDocument> resultList = new List<HtmlDocument>();
            resultList.Add(result);
            _CompletedReferences.Add(reference, resultList);
            Console.WriteLine($"Successfully built '{reference.Key}'");
            return result;
        }

        private static List<HtmlDocument> Build(Reference reference)
        {
            if (_CompletedReferences.ContainsKey(reference))
            {
                return _CompletedReferences[reference];
            }

            if (reference.PathType == PathResult.Folder)
            {
                return BuildFolder(reference);
            }
            else
            {
                List<HtmlDocument> result = new List<HtmlDocument>();
                result.Add(BuildFile(reference));
                return result;
            }
        }

        private static void BuildReferenceTree(Reference head)
        {
            Queue<Reference> toExplore = new();
            HashSet<Reference> explored = new();
            Stack<Reference> toBuild = new();

            toExplore.Enqueue(head);
            explored.Add(head);

            // Discovery
            while (toExplore.Count > 0)
            {
                Reference current = toExplore.Dequeue();
                explored.Add(current);
                toBuild.Push(current);

                foreach (Mapping mapping in current.Contributors)
                {
                    if (explored.Contains(mapping.Contributor)) continue;

                    toExplore.Enqueue(mapping.Contributor);
                }
            }

            // Build
            while (toBuild.Count > 0)
            {
                Reference current = toBuild.Pop();
                Build(current);
            }
        }

        private static void CheckExistingFiles()
        {
            List<(string file, Reference head)> existingFiles = new();
            foreach (Reference head in Mapper.Heads)
            {
                string outputFile = Path.Combine(Program.OutputPath, head.Key + ".html");
                if (File.Exists(outputFile))
                {
                    existingFiles.Add((outputFile, head));
                }
            }

            if (existingFiles.Count > 0)
            {
                StringBuilder output = new(128);
                output.AppendLine("The following files already exist:");
                foreach ((string file, Reference head) in existingFiles)
                {
                    output.AppendLine($"\t{file} (from reference '{head.Key}')");
                }
                output.AppendLine("Do you want to overwrite them? y/n");
                Console.WriteLine(output);

                while (true)
                {
                    string? input = Console.ReadLine();
                    if (input == "y") break;
                    if (input == "n")
                    {
                        Console.WriteLine($"Cancelling operation.");
                        return;
                    }
                }
            }
        }

        public static void BuildFiles(Arguments.Argument[] args)
        {
            if (Directory.Exists(Program.OutputPath))
            {
                CheckExistingFiles();
            }
            else
            {
                Directory.CreateDirectory(Program.OutputPath);
            }

            try
            {
                foreach (Reference head in Mapper.Heads)
                {
                    BuildReferenceTree(head);
                }
            }
            catch (Exception)
            {
                _CompletedReferences.Clear();
                throw;
            }

            foreach(Reference head in Mapper.Heads)
            {
                HtmlDocument headDocument = _CompletedReferences[head][0];
                string outputFile = Path.Combine(Program.OutputPath, head.Key + ".html");
                using FileStream headFile = File.Open(outputFile, FileMode.Create);
                headDocument.Save(headFile);
            }
            _CompletedReferences.Clear();
            Console.WriteLine($"Succesfully finished all operations - {Mapper.Heads.Count} files were built. Output: '{Path.GetFullPath(Program.OutputPath)}'");
        }
    }
}
