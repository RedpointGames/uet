namespace Redpoint.Uefs.Commands.Wait
{
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Commands.Mount;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    public static class WaitCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateWaitCommand()
        {
            var options = new Options();
            var command = new Command("wait", "Waits for all pull operations to complete.");
            command.AddAllOptions(options);
            command.AddCommonHandler<WaitCommandInstance>(options);
            return command;
        }

        private sealed class WaitCommandInstance : ICommandInstance
        {
            private readonly IRetryableGrpc _retryableGrpc;
            private readonly IMonitorFactory _monitorFactory;
            private readonly UefsClient _uefsClient;
            private readonly Options _options;

            public WaitCommandInstance(
                IRetryableGrpc retryableGrpc,
                IMonitorFactory monitorFactory,
                UefsClient uefsClient,
                Options options)
            {
                _retryableGrpc = retryableGrpc;
                _monitorFactory = monitorFactory;
                _uefsClient = uefsClient;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                Console.WriteLine($"waiting for all pull operations to complete...");

                // Loop until we have no pending pull operations.
                do
                {
                    var response = await _retryableGrpc.RetryableGrpcAsync(
                        _uefsClient.GetInProgressOperationsAsync,
                        new GetInProgressOperationsRequest(),
                        new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                        context.GetCancellationToken()).ConfigureAwait(false);
                    if (response.OperationId.Count == 0)
                    {
                        Console.WriteLine("all pending pull operations have finished");
                        return 0;
                    }

                    foreach (var operationId in response.OperationId)
                    {
                        var operation = new ObservableOperation<WaitRequest, WaitResponse>(
                            _retryableGrpc,
                            _monitorFactory,
                            _uefsClient.Wait,
                            wait => wait.PollingResponse,
                            new WaitRequest { OperationId = operationId },
                            TimeSpan.FromMinutes(60),
                            context.GetCancellationToken());
                        await operation.RunAndWaitForCompleteAsync().ConfigureAwait(false);
                    }

                    await Task.Delay(250).ConfigureAwait(false);
                }
                while (true);
            }
        }
    }
}
