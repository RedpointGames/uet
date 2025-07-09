namespace UET.Commands.New
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Commands.New.Plugin;

    internal sealed class NewCommand
    {
        public static Command CreateNewCommand(HashSet<Command> globalCommands)
        {
            var subcommands = new List<Command>
            {
                NewPluginCommand.CreateNewPluginCommand(),
            };

            var command = new Command("new", "Create a new Unreal Engine plugin or project. (experimental)");
            command.IsHidden = true;
            foreach (var subcommand in subcommands)
            {
                globalCommands.Add(subcommand);
                command.AddCommand(subcommand);
            }
            globalCommands.Add(command);
            return command;
        }
    }
}
