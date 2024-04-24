namespace UET.Commands.Storage
{
    using Redpoint.CommandLine;
    using System.Collections.Generic;
    using System.CommandLine;
    using UET.Commands.Storage.List;
    using UET.Commands.Storage.Purge;

    internal static class StorageCommand
    {
        public static ICommandLineBuilder RegisterStorageCommand(
            this ICommandLineBuilder rootBuilder,
            HashSet<Command> globalCommands)
        {
            var command = new Command("storage", "View or remove storage used by UET.");
            globalCommands.Add(command);
            rootBuilder.AddUnhandledCommand(
                builder =>
                {
                    builder
                        .RegisterStorageListCommand()
                        .RegisterStoragePurgeCommand()
                        .RegisterStorageAutoPurgeCommand();
                    return command;
                });
            return rootBuilder;
        }
    }
}
