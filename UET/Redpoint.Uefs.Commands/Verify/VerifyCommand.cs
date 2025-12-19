namespace Redpoint.Uefs.Commands.Verify
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Commands.Mount;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    public class VerifyCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<VerifyCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("verify", "Verify the on-demand cache against the backing storage.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<bool> NoWait = new Option<bool>("--no-wait", description: "Start the verify operation on the daemon, but don't poll until it finishes.");
            public Option<bool> Fix = new Option<bool>("--fix", description: "Clear cached chunks that are invalid so that they are re-fetched from remote source when needed.");
        }

        private sealed class VerifyCommandInstance : ICommandInstance
        {
            private readonly ILogger<VerifyCommandInstance> _logger;
            private readonly IRetryableGrpc _retryableGrpc;
            private readonly IMonitorFactory _monitorFactory;
            private readonly UefsClient _uefsClient;
            private readonly Options _options;

            public VerifyCommandInstance(
                ILogger<VerifyCommandInstance> logger,
                IRetryableGrpc retryableGrpc,
                IMonitorFactory monitorFactory,
                UefsClient uefsClient,
                Options options)
            {
                _logger = logger;
                _retryableGrpc = retryableGrpc;
                _monitorFactory = monitorFactory;
                _uefsClient = uefsClient;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var fix = context.ParseResult.GetValueForOption(_options.Fix);
                var noWait = context.ParseResult.GetValueForOption(_options.NoWait);

                _logger.LogInformation("Verifying all cached images...");

                var operation = new ObservableOperation<VerifyRequest, VerifyResponse>(
                    _retryableGrpc,
                    _monitorFactory,
                    _uefsClient.Verify,
                    verify => verify.PollingResponse,
                    new VerifyRequest
                    {
                        Fix = fix,
                        NoWait = noWait,
                    },
                    TimeSpan.FromMinutes(60),
                    context.GetCancellationToken());
                var response = await operation.RunAndWaitForCompleteAsync().ConfigureAwait(false);
                if (response.PollingResponse.Type == PollingResponseType.Backgrounded)
                {
                    _logger.LogInformation("Verify operation will continue in the background (it may have even completed instantly); use the 'wait' command to wait for all pending verify operations.");
                    return 0;
                }

                if (response.PollingResponse.VerifyChunksFixed > 0)
                {
                    _logger.LogInformation($"Successfully verified all packages, {response.PollingResponse.VerifyChunksFixed} chunks were fixed.");
                }
                else
                {
                    _logger.LogInformation($"Successfully verified all packages.");
                }

                return 0;
            }
        }
    }
}
