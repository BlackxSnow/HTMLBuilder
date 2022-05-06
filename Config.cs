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
            try
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
                    XElement optionElement = Query.ForSingle(root, args[0].Value);
                    if (args.Length == 1)
                    {
                        Console.WriteLine($"{optionElement.Name} = {optionElement.Value}");
                        _ConfigStream.Seek(0, SeekOrigin.Begin);
                        return;
                    }
                    string oldValue = optionElement.Value;
                    optionElement.SetValue(args[1].Value);
                    SaveConfig(root);
                    Console.WriteLine($"Successfully set {optionElement.Name} ({oldValue} -> {optionElement.Value})");
                }
                _ConfigStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception)
            {
                _ConfigStream.Seek(0, SeekOrigin.Begin);
                throw;
            }
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
                root.SetElementValue(option, value);
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
                XElement optionsElement = Query.ForSingle(root, option);
                values.Add(optionsElement.Value);
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
