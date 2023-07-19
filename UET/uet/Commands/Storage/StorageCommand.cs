namespace UET.Commands.Storage
{
    using System.Collections.Generic;
    using System.CommandLine;
    using UET.Commands.Storage.List;

    internal enum StorageEntryType
    {
        Generic,
        WriteScratchLayer,
        ExtractedConsoleZip,
        UefsGitSharedBlobs,
        UefsGitSharedDependencies,
        UefsGitSharedIndexCache,
        UefsGitSharedRepository,
        UefsHostPackagesCache,
    }

    internal class StorageEntry
    {
        public required string Id;
        public required string Path;
        public required StorageEntryType Type;
        public required DateTimeOffset LastUsed;
        public required ulong DiskSpaceConsumed;
    }

    internal class StorageCommand
    {
        public static Command CreateStorageCommand(HashSet<Command> globalCommands)
        {
            var subcommands = new List<Command>
            {
                StorageListCommand.CreateListCommand(),
            };

            var command = new Command("storage", "View or remove storage used by UET.");
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
