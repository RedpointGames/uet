namespace UET.Commands.Lore
{
    using Amazon.Runtime.Internal.Util;
    using LoreVcs;
    using LoreVcs.Types.Args;
    using LoreVcs.Types.Events;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Uet.Commands.ParameterSpec;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Text;

    internal sealed class LoreCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<LoreConvertCommand>();

                    var command = new Command("lore", "Commands for adopting Lore, the next-generation open source version control.");
                    return command;
                })
            .Build();
    }

    internal sealed class LoreConvertCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<LoreImportCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("convert", "Convert the full history of an existing Git repository into a new Lore repository.");
                    command.FullDescription =
                        """
                        This command converts the entire history of a Git repository into a Lore repository. If the Lore repository doesn't exist, it is created. If it does exist, the command continues the import based on the Git commit hash that the latest commit in Lore references.

                        You must not use this command on a Lore repository that has commits *not* made by this command. That is, once you start making commits to the Lore repository, you can't continue using this command to incrementally import other changes from Git.

                        After using this command, you can use 'uet lore mirror' to incrementally mirror a Lore repository back to Git. This treats the Lore repository as the source of truth, while allowing you to use existing Git-only tools on the source code.
                        """;
                    return command;
                })
            .Build();

        internal sealed class Options
        {
            public Argument<DirectoryInfo> GitRepository = new Argument<DirectoryInfo>(
                "git-repo",
                description: "The path to the Git repository to convert.");

            public Argument<DirectoryInfo> LoreRepository = new Argument<DirectoryInfo>(
                "lore-repo",
                description: "The path to the new Lore repository to create.");
        }

        private sealed class LoreImportCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<LoreImportCommandInstance> _logger;

            public LoreImportCommandInstance(
                Options options,
                ILogger<LoreImportCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var gitRepository = context.ParseResult.GetValueForArgument(_options.GitRepository)!;
                var loreRepository = context.ParseResult.GetValueForArgument(_options.LoreRepository)!;

                var loreGlobalArgs = new LoreGlobalArgs
                {
                    RepositoryPath = loreRepository.FullName,
                    Offline = true,
                };

                if (!loreRepository.Exists)
                {
                    _logger.LogInformation($"Creating Lore repository at {loreRepository.FullName}...");
                    var exitCode = await Lore.RepositoryCreate(
                        loreGlobalArgs,
                        new LoreRepositoryCreateArgs
                        {
                            RepositoryUrl = $"lore://localhost/{loreRepository.Name}",
                        }).WaitAsync();
                    if (exitCode != 0)
                    {
                        _logger.LogError($"'lore repository create' failed with exit code {exitCode}.");
                        return exitCode;
                    }
                }

                /*
                var currentRevisionNumber =
                    (await Lore.RepositoryStatus(
                        loreGlobalArgs,
                        new LoreRepositoryStatusArgs
                        {
                            RevisionOnly = true,
                        }).CollectAsync())
                    .OfType<LoreRepositoryStatusRevisionEventData>()
                    .Select(x => x.RevisionLocalNumber)
                    .FirstOrDefault();
                */
                var currentRevision =
                    (await Lore.RevisionInfo(
                        loreGlobalArgs,
                        new LoreRevisionInfoArgs
                        {
                            Revision = string.Empty,
                        }).CollectAsync());
                var message = currentRevision.OfType<LoreMetadataEventData>().FirstOrDefault(x => x.Key == "message")!.Value.String;

                _logger.LogInformation(message);

                foreach (var ev in currentRevision)
                {
                    _logger.LogInformation(ev.GetType().FullName);
                    /*if (ev is LoreRepositoryStatusRevisionEventData statusData)
                    {
                        _logger.LogInformation(Encoding.UTF8.GetString(statusData.Revision));
                    }*/
                }

                return 0;
            }
        }
    }
}
