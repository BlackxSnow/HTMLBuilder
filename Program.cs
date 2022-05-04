using System.Text;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace HTMLBuilder
{
    public static class Program
    {
        const string VERSION = "1.0.0";

        public const string CONFIG_FILE = "HBConfig.xml";

        static bool _IsRunning = true;

        public static string IndexPath { get; set; } = null!;
        public static string ProjectPath { get; set; } = null!;
        public static string OutputPath { get; set; } = null!;

        class Subcommand
        {
            public readonly string Args;
            public readonly string Description;

            public Subcommand(string args, string description)
            {
                Args = args;
                Description = description;
            }
        }

        class Command
        {
            public readonly Action<Argument[]> Function;
            public readonly string Args;
            public readonly string Description;
            public readonly Subcommand[] Subcommands;

            public Command(Action<Argument[]> func, string args, string description, params Subcommand[] subcommands)
            {
                Function = func;
                Args = args;
                Description = description;
                Subcommands = subcommands;
            }
        }

        public class Argument
        {
            public string Value;
            public bool IsOption;

            public Argument(string value, bool isOption)
            {
                Value = value;
                IsOption = isOption;
            }
        }

        static readonly Dictionary<string, Command> _Commands = new()
        {
            { "help", new Command(Help, "[?string:command]", "Lists commands or provides detailed info on given command") },
            { "build", new Command(BuildIndex, "", "Generates index.html from provided data.") },
            { "config", new Command(Config.Configure, "[?string:option] [?string:value]", "Gets or sets config options. Provide value to set, empty to list.") },
            { "exit", new Command((_) => _IsRunning = false, "", "Quits the application.") },
            { "ref", new Command(Referencing.Command_Ref, "[set/remove/list]", "Mapping Reference manipulation and viewing.",
                new Subcommand("set [key] [file/folder] [path]", "Creates or modifies a reference by key."),
                new Subcommand("remove", "Removes a mapping reference."),
                new Subcommand("list [?string:search] [options: (-p --path), (-m --mappings)]", "Lists all mapping references optionally by search term.")
            )}
        };

        static void HandleInvalid()
        {
            Console.WriteLine("Invalid input.");
            _Commands["help"].Function(Array.Empty<Argument>());
        }

        static Argument[] ParseArgs(IEnumerable<string> argStrings)
        {
            List<Argument> arguments = new(argStrings.Count());
            foreach (var arg in argStrings)
            {
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    arguments.Add(new Argument(arg.Trim('-'), true));
                }
                else
                {
                    arguments.Add(new Argument(arg, false));
                }
            }
            return arguments.ToArray();
        }

        static void HandleCommand(string input)
        {
            string[] values = input.Split(' ', StringSplitOptions.TrimEntries);
            Argument[] args = ParseArgs(values.Skip(1));

            if (_Commands.TryGetValue(values[0].ToLower(), out var command))
            {
                try
                {
                    command.Function(args);
                }
                catch (ArgumentException)
                {
                }
            }
            else
            {
                HandleInvalid();
                return;
            }
        }

        static void Main(string[] _)
        {
            Console.WriteLine($"HTML Inserter - Version {VERSION}\nType 'help' to list commands.");

            Config.Initialize();
            Map.Initialize();

            while (_IsRunning)
            {
                string? input = Console.ReadLine();
                if (input == null)
                {
                    HandleInvalid();
                    continue;
                }
                HandleCommand(input);
            }
        }

        static void Help(Argument[] args)
        {
            StringBuilder output = new(10 + _Commands.Count * 50);
            output.AppendLine("Help:");

            foreach (KeyValuePair<string, Command> command in _Commands)
            {
                output.AppendLine($"\t{command.Key}: {command.Value.Description}");
            }
            Console.WriteLine(output.ToString());
        }



        static void InsertProject(HtmlNode indexHeaderContainer, HtmlNode indexProjectContainer, HtmlNode projectHeader, HtmlNode projectBody)
        {
            indexHeaderContainer.AppendChildren(projectHeader.ChildNodes);

            HtmlNode lastChild = indexHeaderContainer;
            foreach(HtmlNode node in projectBody.ChildNodes)
            {
                indexProjectContainer.InsertAfter(node, lastChild);
                lastChild = node;
            }
        }

        static void InsertProjects(HtmlNode projectHeaderContainer, HtmlNode projectsContainer)
        {
            string[] projectFiles = Directory.GetFiles(ProjectPath);

            for (int i = projectFiles.Length - 1; i >= 0; i--)
            {
                string file = projectFiles[i];
                if (!file.EndsWith(".html"))
                {
                    Console.WriteLine($"Skipping '{file}' due to extension.");
                    continue;
                }
                HtmlDocument projectRoot = new();
                projectRoot.Load(file);

                bool isHeaderValid = Query.Perform(projectRoot, "headerData", out var headerQuery);
                bool isBodyValid = Query.Perform(projectRoot, "bodyData", out var bodyQuery);
                if (!isHeaderValid || !isBodyValid)
                {
                    Console.WriteLine($"Skipping '{file}' due to missing header or body elements.");
                    continue;
                }
                InsertProject(projectHeaderContainer, projectsContainer, headerQuery.First(), bodyQuery.First());
                Console.WriteLine($"Successfully inserted '{file}");
            }
        }

        static void BuildIndex(Argument[] args)
        {
            if (!File.Exists(IndexPath))
            {
                Console.WriteLine($"ERROR: Could not find template index.html at {Path.GetFullPath(IndexPath)}");
                return;
            }
            HtmlDocument root = new();
            try
            {
                root.Load(IndexPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
                return;
            }

            bool isBodyValid = Query.Perform(root, "div", "main", out IEnumerable<HtmlNode> bodyQuery);
            bool isHeaderValid = Query.Perform(root, "article", "projects", out IEnumerable<HtmlNode> headerQuery);
            if (!isBodyValid || !isHeaderValid)
            {
                Console.WriteLine("Operation failed.");
                return;
            }

            HtmlNode body = bodyQuery.First();
            HtmlNode header = headerQuery.First();

            InsertProjects(header, body);

            FileStream outputFile = File.Create(OutputPath);
            root.Save(outputFile);
            outputFile.Close();
            Console.WriteLine($"Successfully saved file to '{Path.GetFullPath(OutputPath)}'");
        }
    } 
}