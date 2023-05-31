namespace UET.Commands.Internal.RunAutomationTestFromBuildGraph
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class RunAutomationTestFromBuildGraphCommand
    {
        internal class Options
        {
            public Option<string> EnginePath;
            public Option<string> TestProjectPath;
            public Option<string> TestPrefix;
            public Option<int?> MinWorkerCount;
            public Option<int?> TimeoutMinutes;
            public Option<string> TestResultsPath;

            public Options()
            {
                EnginePath = new Option<string>("--engine-path");
                TestProjectPath = new Option<string>("--test-project-path");
                TestPrefix = new Option<string>("--test-prefix");
                MinWorkerCount = new Option<int?>("--min-worker-count");
                TimeoutMinutes = new Option<int?>("--timeout-minutes");
                TestResultsPath = new Option<string>("--test-results-path");
            }
        }

        public static Command CreateRunAutomationTestFromBuildGraphCommand()
        {
            var options = new Options();
            var command = new Command("run-automation-test-from-buildgraph");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunAutomationTestFromBuildGraphCommandInstance>(options);
            return command;
        }

        private class RunAutomationTestFromBuildGraphCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunAutomationTestFromBuildGraphCommandInstance> _logger;

            public RunAutomationTestFromBuildGraphCommandInstance(
                ILogger<RunAutomationTestFromBuildGraphCommandInstance> logger)
            {
                _logger = logger;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                _logger.LogError("Not yet implemented.");
                return Task.FromResult(1);
            }
        }
    }
}
