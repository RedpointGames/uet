namespace UET.Commands.Internal.TestAutoDiscovery
{
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.Concurrency;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class TestAutoDiscoveryCommand
    {
        internal class Options
        {
        }

        public static Command CreateTestAutoDiscoveryCommand()
        {
            var options = new Options();
            var command = new Command("test-autodiscovery");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestAutoDiscoveryCommandInstance>(options);
            return command;
        }

        private class TestAutoDiscoveryCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestAutoDiscoveryCommandInstance> _logger;
            private readonly INetworkAutoDiscovery _networkAutoDiscovery;

            public TestAutoDiscoveryCommandInstance(
                ILogger<TestAutoDiscoveryCommandInstance> logger,
                INetworkAutoDiscovery networkAutoDiscovery)
            {
                _logger = logger;
                _networkAutoDiscovery = networkAutoDiscovery;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                await using (await _networkAutoDiscovery.RegisterServiceAsync(
                    $"{Environment.MachineName}._discoverytest._tcp.local",
                    10203,
                    context.GetCancellationToken()))
                {
                    _logger.LogInformation("Auto-discovery service registered.");
                    var gate = new Gate();
                    context.GetCancellationToken().Register(() =>
                    {
                        gate.Open();
                    });
                    await gate.WaitAsync(context.GetCancellationToken());
                    return 0;
                }
            }
        }
    }
}
