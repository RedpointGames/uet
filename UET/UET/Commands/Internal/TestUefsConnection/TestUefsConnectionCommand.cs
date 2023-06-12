namespace UET.Commands.Internal.TestUefsConnection
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using static Uefs.UEFS;

    internal class TestUefsConnectionCommand
    {
        internal class Options
        {
        }

        public static Command CreateTestUefsConnectionCommand()
        {
            var options = new Options();
            var command = new Command("test-uefs-connection");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestUefsConnectionCommandInstance>(options);
            return command;
        }

        private class TestUefsConnectionCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestUefsConnectionCommandInstance> _logger;
            private readonly UEFSClient _uefsClient;

            public TestUefsConnectionCommandInstance(
                ILogger<TestUefsConnectionCommandInstance> logger,
                UEFSClient uefsClient)
            {
                _logger = logger;
                _uefsClient = uefsClient;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var results = await _uefsClient.ListAsync(new Uefs.ListRequest());
                if (!string.IsNullOrWhiteSpace(results.Err))
                {
                    _logger.LogError($"Error while listing UEFS mounts: {results.Err}");
                    return 1;
                }
                _logger.LogInformation($"There are {results.Mounts.Count} mounts on this system.");
                foreach (var result in results.Mounts)
                {
                    _logger.LogInformation($" - {result.Id}");
                }
                return 0;
            }
        }
    }
}
