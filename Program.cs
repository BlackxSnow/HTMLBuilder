using System.Text;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace HTMLBuilder
{
    public static class Program
    {
        const string VERSION = "1.2.2";

        public const string CONFIG_FILE = "HBConfig.xml";

        static bool _IsRunning = true;

        public static string IndexPath { get; set; } = null!;
        public static string ProjectPath { get; set; } = null!;
        public static string OutputPath { get; set; } = null!;
        

        private static void HandleInvalid()
        {
            Console.WriteLine("Invalid input.");
            Commands.Get("help").Function(Array.Empty<Arguments.Argument>());
        }

        private static Arguments.Argument[] ParseArgs(IEnumerable<string> argStrings)
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
                bool prependToNext = i < quoteSplit.Length - 1 && current[^1] != ' ';

                if (appendToLast)
                {
                    finalArgs[^1] += spaceSplit[start++];
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

            if (Commands.TryGet(values[0].ToLower(), out var command))
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

        public static void Quit(Arguments.Argument[] _)
        {
            _IsRunning = false;
        }
        
        static void Main(string[] _)
        {
            Console.WriteLine($"HTML Inserter - Version {VERSION}\nType 'help' to list commands.");

            Config.Initialize();
            Mapper.Initialize();
            Commands.Initialise();

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


    } 
}