using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HTMLBuilder
{
    public static class Config
    {
        static FileStream _ConfigStream = null!;

        public static (string option, string value)[] DefaultConfig { get; private set; } = new (string option, string value)[]
        {
            ("OutputPath", "output")
        };

        public static void Configure(Arguments.Argument[] args)
        {
            XElement root = XElement.Load(_ConfigStream);
            if (args.Length == 0)
            {
                StringBuilder output = new(128);
                output.AppendLine("Config Options:");
                foreach (XElement element in root.Elements())
                {
                    output.AppendLine($"\t{element.Name} = {element.Value}");
                }
                Console.WriteLine(output.ToString());
            }
            else
            {
                bool isValid = Query.Perform(root, args[0].Value, out var optionQuery, true);
                if (!isValid)
                {
                    _ConfigStream.Seek(0, SeekOrigin.Begin);
                    return;
                }
                if (optionQuery.Count() > 1)
                {
                    StringBuilder output = new(128);
                    output.AppendLine($"Key '{args[0].Value}' was ambiguous between:");
                    foreach (XElement element in optionQuery)
                    {
                        output.AppendLine($"\t{element.Name} = {element.Value}");
                    }
                    Console.WriteLine(output);
                    _ConfigStream.Seek(0, SeekOrigin.Begin);
                    return;
                }

                XElement option = optionQuery.First();
                if (args.Length == 1)
                {
                    Console.WriteLine($"{option.Name} = {option.Value}");
                    _ConfigStream.Seek(0, SeekOrigin.Begin);
                    return;
                }
                string oldValue = option.Value;
                option.SetValue(args[1].Value);
                SaveConfig(root);
                Console.WriteLine($"Successfully set {option.Name} ({oldValue} -> {option.Value})");
            }
            _ConfigStream.Seek(0, SeekOrigin.Begin);
        }

        static void SaveConfig(XElement root)
        {
            _ConfigStream.SetLength(0);
            root.Save(_ConfigStream);
            _ConfigStream.Flush();
            _ConfigStream.Seek(0, SeekOrigin.Begin);
            Load();
        }

        public static void Write(XElement? root, params (string option, string value)[] toWrite)
        {
            if (root == null)
            {
                root = XElement.Load(_ConfigStream);
            }

            foreach ((string option, string value) in toWrite)
            {
                IEnumerable<XElement> query = from element in root.Elements(option) select element;
                if (query.Count() > 1)
                {
                    throw new ArgumentException($"Too many results for '{option}'");
                }
                else if (!query.Any())
                {
                    root.SetElementValue(option, value);
                }

                query.First().SetValue(value);
            }
            SaveConfig(root);
        }

        public static IEnumerable<XElement> ReadAll()
        {
            return XElement.Load(_ConfigStream).Elements();
        }

        public static string[] Read(XElement? root, params string[] options)
        {
            List<string> values = new();
            if (root == null)
            {
                root = XElement.Load(_ConfigStream);
            }
            foreach (string option in options)
            {
                IEnumerable<XElement> query = from element in root.Elements(option) select element;
                if (query.Count() > 1)
                {
                    throw new ArgumentException($"Too many results for '{option}'");
                }
                else if (!query.Any())
                {
                    throw new ArgumentException($"No results for '{option}'");
                }
                values.Add(query.First().Value);
            }
            _ConfigStream.Seek(0, SeekOrigin.Begin);
            return values.ToArray();
        }

        static void Validate(XElement? root = null)
        {
            if (root == null)
            {
                root = _ConfigStream.Length > 0 ? XElement.Load(_ConfigStream) : new XElement("config");
            }
            foreach ((string option, string value) in DefaultConfig)
            {
                if (!(from element in root.Elements(option) select element).Any())
                {
                    root.SetElementValue(option, value);
                    Console.WriteLine($"Option '{option}' was missing from config and was added with default value '{value}'.");
                }
            }

            SaveConfig(root);
        }

        static void Load()
        {
            string[] configValues = Config.Read(null, Config.DefaultConfig.Select(c => c.option).ToArray());
            Program.OutputPath = configValues[0];
        }

        public static void Initialize()
        {
            if (!File.Exists(Program.CONFIG_FILE))
            {
                Console.WriteLine($"Config file {Program.CONFIG_FILE} was not found, generating...");
                _ConfigStream = File.Create(Program.CONFIG_FILE);
                XElement root = new("config", null!);
                Write(root, DefaultConfig);
                Console.WriteLine($"Successfully generated config.");
            }
            else
            {
                _ConfigStream = File.Open(Program.CONFIG_FILE, FileMode.Open);
                Validate();
            }
            Load();
        }
    }
}
