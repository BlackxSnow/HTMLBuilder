using System.Text;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace HTMLBuilder
{
    public static class Program
    {
        const string VERSION = "1.2.0";

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
            public readonly Action<Arguments.Argument[]> Function;
            public readonly string Args;
            public readonly string Description;
            public readonly Subcommand[] Subcommands;

            public Command(Action<Arguments.Argument[]> func, string args, string description, params Subcommand[] subcommands)
            {
                Function = func;
                Args = args;
                Description = description;
                Subcommands = subcommands;
            }
        }

        static readonly Dictionary<string, Command> _Commands = new()
        {
            { "help", new Command(Help, "[?string:command]", "Lists commands or provides detailed info on given command") },
            { "build", new Command(Builder.BuildFiles, "", "Builds outputs from provided mappings.") },
            { "config", new Command(Config.Configure, "[?string:option] [?string:value]", "Gets or sets config options. Provide value to set, empty to list.") },
            { "exit", new Command((_) => _IsRunning = false, "", "Quits the application.") },
            { "ref", new Command(Referencing.Command_Ref, "[set/remove/list]", "Mapping Reference manipulation and viewing.",
                new Subcommand("set [key] [file/folder] [path]", "Creates or modifies a reference by key."),
                new Subcommand("remove", "Removes a mapping reference."),
                new Subcommand("list [?string:search] [options: (-p --path), (-m --mappings)]", "Lists all mapping references optionally by search term.")
            )},
            { "map", new Command(Mapper.Command_Map, "[set/remove/list]", "Manipulation and viewing of mappings.",
                new Subcommand("set [key] [file/folder] [path]", "Creates or modifies a mapping by key."),
                new Subcommand("remove", "Removes a mapping."),
                new Subcommand("list [?string:search] [options: (-v --verbose)]", "Lists all mappings grouped by consumer, and optionally filtered by search term.")
            )},
            { "deploy", new Command(SFTPDeployer.Deploy, "", "Recursive deployment of directory to FTP server.") }
        };

        static void HandleInvalid()
        {
            Console.WriteLine("Invalid input.");
            _Commands["help"].Function(Array.Empty<Arguments.Argument>());
        }

        static Arguments.Argument[] ParseArgs(IEnumerable<string> argStrings)
        {
            List<Arguments.Argument> arguments = new(argStrings.Count());
            foreach (var arg in argStrings)
            {
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    arguments.Add(new Arguments.Argument(arg.Trim('-'), true));
                }
                else
                {
                    arguments.Add(new Arguments.Argument(arg, false));
                }
            }
            return arguments.ToArray();
        }

        private static string[] ParseArgString(string input)
        {
            string[] quoteSplit = input.Split('"');
            List<string> finalArgs = new();

            for (int i = 0; i < quoteSplit.Length; i++)
            {
                string current = quoteSplit[i];
                if (i % 2 != 0)
                {
                    finalArgs.Add(current);
                    continue;
                }

                if (current == "") continue;

                string[] spaceSplit = current.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                int start = 0;
                int end = spaceSplit.Length;
                bool appendToLast = i > 0 && current[0] != ' ';
                bool prependToNext = i < quoteSplit.Length - 1 && current[current.Length - 1] != ' ';

                if (appendToLast)
                {
                    finalArgs[finalArgs.Count - 1] += spaceSplit[start++];
                }
                if (prependToNext)
                {
                    quoteSplit[i + 1] = spaceSplit[--end] + quoteSplit[i + 1];
                }

                finalArgs.AddRange(spaceSplit.Skip(start).Take(end - start));
            }
            return finalArgs.ToArray();
        }

        static void HandleCommand(string input)
        {
            string[] values = ParseArgString(input);
            Arguments.Argument[] args = ParseArgs(values.Skip(1));

            if (values.Length == 0) return;

            if (_Commands.TryGetValue(values[0].ToLower(), out var command))
            {
                try
                {
                    command.Function(args);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
                catch (BuilderException ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
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
            Mapper.Initialize();

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

        static void Help(Arguments.Argument[] args)
        {
            StringBuilder output = new(10 + _Commands.Count * 50);
            output.AppendLine("Help:");

            foreach (KeyValuePair<string, Command> command in _Commands)
            {
                output.AppendLine($"\t{command.Key}: {command.Value.Description}");
            }
            Console.WriteLine(output.ToString());
        }
    } 
}