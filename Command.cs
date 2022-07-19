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

        public Option()
        {
            Short = "";
            Long = "";
            Args = "";
            Description = "";
        }
    }
    public class Subcommand
    {
        public string Name;
        public string Args;
        public Option[] Options;
        public string Description;

        public Subcommand()
        {
            Name = "";
            Args = "";
            Options = Array.Empty<Option>();
            Description = "";
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
        Name = "";
        Args = "";
        Options = Array.Empty<Option>();
        Description = "";
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
    
    private static void PrintOption(StringBuilder output, Command.Option option, string prefix = "\t")
    {
        output.Append($"{prefix}-{option.Short}, --{option.Long} ");
        if (option.Args.Length > 0) output.Append($"{option.Args} ");
        output.AppendLine($"{option.Description}");
    }
    
    private static void PrintSubcommand(StringBuilder output, Command.Subcommand sub)
    {
        output.Append($"\t{sub.Name}");
        if (sub.Args.Length > 0) output.Append($" {sub.Args}");
        output.AppendLine($" [Options...]");
        output.AppendLine($"\t\t{sub.Description}");
        if (sub.Options.Length == 0) return;
        
        output.AppendLine("\t\tOptions:");
        foreach (var option in sub.Options)
        {
            PrintOption(output, option, "\t\t\t");
        }
    }
    
    private static StringBuilder PrintCommand(Command command)
    {
        var output = new StringBuilder(200);
        output.AppendLine($"Help ({command.Name}):");
        output.AppendLine("Usage:");
        output.Append($"\t{command.Name} ");
        if (command.Subcommands.Length > 0)
        {
            for (var i = 0; i < command.Subcommands.Length; i++)
            {
                output.Append(command.Subcommands[i].Name);
                if (i != command.Subcommands.Length - 1) output.Append("|");
            }
        }
        else if (command.Args.Length > 0)
        {
            output.Append($" {command.Args}");
        }
        output.AppendLine(" [Options...]");
        output.AppendLine(command.Description);

        if (command.Options.Length > 0) output.AppendLine("Options:");
        foreach (var option in command.Options)
        {
            PrintOption(output, option);
        }

        if (command.Subcommands.Length == 0) return output;
        
        output.AppendLine("Subcommands: ");
        foreach (var sub in command.Subcommands)
        {
            PrintSubcommand(output, sub);
        }
        return output;
    }

    private static StringBuilder PrintAll()
    {
        StringBuilder output = new(10 + _Commands.Count * 50);
        output.AppendLine("Help:");

        foreach (KeyValuePair<string, Command> command in _Commands)
        {
            output.AppendLine($"\t{command.Key}: {command.Value.Description}");
        }

        return output;
    }
    
    private static void PrintHelp(Arguments.Argument[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine(Commands.TryGet(args[0].Value, out var command)
                ? PrintCommand(command).ToString()
                : $"Unknown command '{args[0].Value}'");
        }
        else
        {
            Console.WriteLine(PrintAll().ToString());
        }
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
