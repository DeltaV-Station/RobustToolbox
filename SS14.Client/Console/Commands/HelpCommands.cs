﻿using SS14.Client.Interfaces.Console;
using SS14.Shared.Console;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.IoC;
using SS14.Shared.Maths;

namespace SS14.Client.Console.Commands
{
    class HelpCommand : IConsoleCommand
    {
        public string Command => "help";
        public string Help => "When no arguments are provided, displays a generic help text. When an argument is passed, display the help text for the command with that name.";
        public string Description => "Display help text.";

        public bool Execute(IDebugConsole console, params string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    console.AddLine("To display help for a specific command, write 'help <command>'. To list all available commands, write 'list'.");
                    break;

                case 1:
                    string commandname = args[0];
                    if (!console.Commands.ContainsKey(commandname))
                    {
                        if (!IoCManager.Resolve<IClientNetManager>().IsConnected)
                        {
                            // No server so nothing to respond with unknown command.
                            console.AddLine("Unknown command: " + commandname, Color.Red);
                            return false;
                        }
                        // TODO: Maybe have a server side help?
                        return false;
                    }
                    IConsoleCommand command = console.Commands[commandname];
                    console.AddLine(string.Format("{0} - {1}", command.Command, command.Description));
                    console.AddLine(command.Help);
                    break;

                default:
                    console.AddLine("Invalid amount of arguments.", Color.Red);
                    break;
            }
            return false;
        }
    }

    class ListCommand : IConsoleCommand
    {
        public string Command => "list";
        public string Help => "Lists all available commands, and their short descriptions.";
        public string Description => "List all commands";

        public bool Execute(IDebugConsole console, params string[] args)
        {
            foreach (IConsoleCommand command in console.Commands.Values)
            {
                console.AddLine(command.Command + ": " + command.Description);
            }

            return false;
        }
    }
}
