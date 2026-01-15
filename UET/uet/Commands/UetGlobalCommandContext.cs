namespace UET.Commands
{
    using System.CommandLine;

    internal class UetGlobalCommandContext
    {
        private HashSet<Command> _commandsUsingBuildConfigUetVersion = new();

        public void CommandRequiresUetVersionInBuildConfig(Command command)
        {
            _commandsUsingBuildConfigUetVersion.Add(command);
        }

        public bool IsGlobalCommand(Command executingCommand)
        {
            return _commandsUsingBuildConfigUetVersion.Contains(executingCommand);
        }
    }
}
