namespace UET.Commands.Internal.EngineCheckout
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class EngineCheckoutCommand
    {
        internal sealed class Options
        {
            public Option<string> Path;
            public Option<string> RepositoryUri;
            public Option<string> Branch;

            public Options()
            {
                Path = new Option<string>("--path");
                RepositoryUri = new Option<string>("--uri");
                Branch = new Option<string>("--branch");
            }
        }

        public static Command CreateEngineCheckoutCommand()
        {
            var options = new Options();
            var command = new Command("engine-checkout");
            command.AddAllOptions(options);
            command.AddCommonHandler<EngineCheckoutCommandInstance>(options);
            return command;
        }

        private sealed class EngineCheckoutCommandInstance : ICommandInstance
        {
            private readonly ILogger<EngineCheckoutCommandInstance> _logger;
            private readonly IPhysicalGitCheckout _physicalGitCheckout;
            private readonly Options _options;

            public EngineCheckoutCommandInstance(
                ILogger<EngineCheckoutCommandInstance> logger,
                IPhysicalGitCheckout physicalGitCheckout,
                Options options)
            {
                _logger = logger;
                _physicalGitCheckout = physicalGitCheckout;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                Directory.CreateDirectory(context.ParseResult.GetValueForOption(_options.Path)!);

                await _physicalGitCheckout.PrepareGitWorkspaceAsync(
                    context.ParseResult.GetValueForOption(_options.Path)!,
                    new GitWorkspaceDescriptor
                    {
                        RepositoryUrl = context.ParseResult.GetValueForOption(_options.RepositoryUri)!,
                        RepositoryCommitOrRef = context.ParseResult.GetValueForOption(_options.Branch)!,
                        AdditionalFolderLayers = [],
                        AdditionalFolderZips = [],
                        WorkspaceDisambiguators = [],
                        WindowsSharedGitCachePath = null,
                        MacSharedGitCachePath = null,
                        ProjectFolderName = null,
                        BuildType = GitWorkspaceDescriptorBuildType.EngineLfs,
                    },
                    context.GetCancellationToken()).ConfigureAwait(false);
                return 0;
            }
        }
    }
}
