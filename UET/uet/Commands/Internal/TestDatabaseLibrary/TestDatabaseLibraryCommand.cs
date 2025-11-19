namespace UET.Commands.Internal.TestDatabaseLibrary
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Database;
    using Redpoint.Uet.Database.Models;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using TestPipes;
    using static TestPipes.TestService;

    internal sealed class TestDatabaseLibraryCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateTestDatabaseLibraryCommand()
        {
            var options = new Options();
            var command = new Command("test-database-library");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestDatabaseLibraryCommandInstance>(options);
            return command;
        }

        private sealed class TestDatabaseLibraryCommandInstance : ICommandInstance
        {
            private readonly IUetDbConnectionFactory _databaseConnectionFactory;
            private readonly ILogger<TestDatabaseLibraryCommandInstance> _logger;

            public TestDatabaseLibraryCommandInstance(
                IUetDbConnectionFactory databaseConnectionFactory,
                ILogger<TestDatabaseLibraryCommandInstance> logger)
            {
                _databaseConnectionFactory = databaseConnectionFactory;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                await using var databaseConnection =
                    await _databaseConnectionFactory.ConnectToDefaultDatabaseAsync(context.GetCancellationToken());

                _logger.LogInformation("Listing all last engine path entries...");
                await foreach (var entry in databaseConnection.ListAsync<LastEnginePathModel>(context.GetCancellationToken()))
                {
                    _logger.LogInformation($"{entry.Key} = {entry.LastEnginePath}");
                }

                return 0;
            }
        }
    }
}
