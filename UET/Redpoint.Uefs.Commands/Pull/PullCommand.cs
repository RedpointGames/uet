namespace Redpoint.Uefs.Commands.Pull
{
    using Grpc.Core;
    using Redpoint.CredentialDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Commands.Mount;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    public static class PullCommand
    {
        internal class Options
        {
            public Option<string> PackageTag = new Option<string>("--tag", description: "The registry tag to pull.");
            public Option<string> GitUrl = new Option<string>("--git-url", description: "The Git repository URL to pull.");
            public Option<string> GitCommit = new Option<string>("--git-commit", description: "The Git commit to pull.");
            public Option<bool> NoWait = new Option<bool>("--no-wait", description: "Start the pull operation on the daemon, but don't poll until it finishes.");
        }

        public static Command CreatePullCommand()
        {
            var options = new Options();
            var command = new Command("pull", "Pulls the latest UEFS package from a registry.");
            command.AddAllOptions(options);
            command.AddCommonHandler<PullCommandInstance>(options);
            return command;
        }

        private class PullCommandInstance : ICommandInstance
        {
            private readonly ICredentialDiscovery _credentialDiscovery;
            private readonly IRetryableGrpc _retryableGrpc;
            private readonly IMonitorFactory _monitorFactory;
            private readonly UefsClient _uefsClient;
            private readonly Options _options;

            public PullCommandInstance(
                ICredentialDiscovery credentialDiscovery,
                IRetryableGrpc retryableGrpc,
                IMonitorFactory monitorFactory,
                UefsClient uefsClient,
                Options options)
            {
                _credentialDiscovery = credentialDiscovery;
                _retryableGrpc = retryableGrpc;
                _monitorFactory = monitorFactory;
                _uefsClient = uefsClient;
                _options = options;
            }

            private async Task<PullResponse> PullAsync<TRequest>(
                Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<PullResponse>> call,
                TRequest request,
                CancellationToken cancellationToken)
            {
                var operation = new ObservableOperation<TRequest, PullResponse>(
                    _retryableGrpc,
                    _monitorFactory,
                    call,
                    response => response.PollingResponse,
                    request,
                    TimeSpan.FromMinutes(60),
                    cancellationToken);
                return await operation.RunAndWaitForCompleteAsync().ConfigureAwait(false);
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var packageTag = context.ParseResult.GetValueForOption(_options.PackageTag);
                var gitUrl = context.ParseResult.GetValueForOption(_options.GitUrl);
                var gitCommit = context.ParseResult.GetValueForOption(_options.GitCommit);
                var noWait = context.ParseResult.GetValueForOption(_options.NoWait);

                try
                {
                    PullResponse response;
                    if (!string.IsNullOrWhiteSpace(packageTag) &&
                         string.IsNullOrWhiteSpace(gitUrl) &&
                         string.IsNullOrWhiteSpace(gitCommit))
                    {
                        response = await PullAsync(
                            _uefsClient.PullPackageTag,
                            new PullPackageTagRequest
                            {
                                PullRequest = new PullRequest
                                {
                                    NoWait = noWait,
                                },
                                Tag = packageTag,
                                Credential = _credentialDiscovery.GetRegistryCredential(packageTag!),
                            },
                            context.GetCancellationToken()).ConfigureAwait(false);
                    }
                    else if (string.IsNullOrWhiteSpace(packageTag) &&
                             !string.IsNullOrWhiteSpace(gitUrl) &&
                             !string.IsNullOrWhiteSpace(gitCommit))
                    {
                        response = await PullAsync(
                            _uefsClient.PullGitCommit,
                            new PullGitCommitRequest
                            {
                                PullRequest = new PullRequest
                                {
                                    NoWait = noWait,
                                },
                                Url = gitUrl,
                                Commit = gitCommit,
                                Credential = _credentialDiscovery.GetGitCredential(gitUrl),
                            },
                            context.GetCancellationToken()).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.Error.WriteLine("error: expected --tag or both --git-url and --git-commit");
                        return 1;
                    }

                    if (response.PollingResponse.Type == PollingResponseType.Backgrounded)
                    {

                    }

                    return 0;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"error: failed to pull: {ex.Message}");
                    return 1;
                }
            }
        }
    }
}
