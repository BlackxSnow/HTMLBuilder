using System.Reflection;
using System.Text;

namespace HTMLBuilder;

public class Command
{
    public class Option
    {
        public string Short;
        public string Long;
        public string Args;
        public string Description;
    }
    public class Subcommand
    {
        public string Name;
        public string Args;
        public Option[] Options;
        public string Description;

        public Subcommand()
        {
            Options = Array.Empty<Option>();
        }
    }

    public string Name;
    public Action<Arguments.Argument[]> Function;
    public string Args;
    public Option[] Options;
    public string Description;
    public Subcommand[] Subcommands;

    public Command()
    {
        Options = Array.Empty<Option>();
        Subcommands = Array.Empty<Subcommand>();
    }
}

public static class Commands
{
    private static Dictionary<string, Command> _Commands = new Dictionary<string, Command>();

    public static bool TryGet(string key, out Command command)
    {
        return _Commands.TryGetValue(key, out command);
    }

    public static Command Get(string key)
    {
        return _Commands[key];
    }

    public static void Initialise()
    {
        var commandsType = typeof(Commands);
        var fields = commandsType.GetFields(BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            var value = field.GetValue(null);
            if (value is not Command command) continue;
            
            _Commands.Add(command.Name, command);
            Console.WriteLine($"Registered command '{command.Name}'");
        }
    }
    
    private static void PrintHelp(Arguments.Argument[] args)
    {
        StringBuilder output = new(10 + _Commands.Count * 50);
        output.AppendLine("Help:");

        foreach (KeyValuePair<string, Command> command in _Commands)
        {
            output.AppendLine($"\t{command.Key}: {command.Value.Description}");
        }
        Console.WriteLine(output.ToString());
    }
    
    private static readonly Command _Help = new Command()
    {
        Name = "help",
        Args = "[?string:command]",
        Description = "Lists commands or provides detailed info on given command",
        Function = PrintHelp
    };

    private static readonly Command _Build = new Command()
    {
        Name = "build",
        Args = "",
        Description = "Builds outputs from provided mappings.",
        Function = Builder.BuildFiles
    };
    
    private static readonly Command _Config = new Command()
    {
        Name = "config",
        Args = "[?string:option] [?string:value]",
        Description = "Gets or sets config options. Provide value to set, empty to list.",
        Function = Config.Configure
    };
    
    private static readonly Command _Exit = new Command()
    {
        Name = "exit",
        Args = "",
        Description = "Quits the application.",
        Function = Program.Quit
    };
    
    private static readonly Command _Ref = new Command()
    {
        Name = "ref",
        Args = "",
        Description = "Mapping Reference manipulation and viewing.",
        Function = Referencing.Command_Ref,
        Subcommands = new []
        {
            new Command.Subcommand()
            {
                Name = "set",
                Args = "[string:key] [file/folder] [string:path]",
                Description = "Creates or modifies a reference by key."
            },
            new Command.Subcommand()
            {
                Name = "remove",
                Args = "[string:key]",
                Description = "Removes an existing mapping reference."
            },
            new Command.Subcommand()
            {
                Name = "list",
                Args = "[?string:search]",
                Description = "Lists all mapping references optionally by search term.",
                Options = new []
                {
                    new Command.Option()
                    {
                        Short = "p",
                        Long = "path",
                        Args = "",
                        Description = "Print reference paths."
                    },
                    new Command.Option()
                    {
                        Short = "m",
                        Long = "mappings",
                        Args = "",
                        Description = "Print reference mappings."
                    }
                }
            }
        }
    };
    
    private static readonly Command _Map = new Command()
    {
        Name = "map",
        Args = "",
        Description = "Manipulation and viewing of mappings.",
        Function = Mapper.Command_Map,
        Subcommands = new []
        {
            new Command.Subcommand()
            {
                Name = "set",
                Args = "[string:key] [file/folder] [string:path]",
                Description = "Creates or modifies a mapping by key."
            },
            new Command.Subcommand()
            {
                Name = "remove",
                Args = "[string:key]",
                Description = "Removes an existing mapping."
            },
            new Command.Subcommand()
            {
                Name = "list",
                Args = "[?string:search]",
                Description = "Lists all mappings grouped by consumer, and optionally filtered by search term.",
                Options = new []
                {
                    new Command.Option()
                    {
                        Short = "v",
                        Long = "verbose",
                        Args = "",
                        Description = "Print mapping queries."
                    }
                }
            }
        }
    };

    private static readonly Command _Deploy = new Command()
    {
        Name = "deploy",
        Args = "",
        Description = "Recursive deployment of directory to FTP server.",
        Function = SFTPDeployer.Deploy,
        Options = new []
        {
            new Command.Option()
            {
                Short = "d",
                Long = "dry",
                Args = "",
                Description = "Perform operation without modifying any local or remote files."
            }
        }
    };
}
