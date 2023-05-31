namespace UET.Commands.Internal.RunGauntletTestFromBuildGraph
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

    internal class RunGauntletTestFromBuildGraphCommand
    {
        internal class Options
        {
            public Option<string> EnginePath;
            public Option<string> Distribution;
            public Option<string> TestName;

            public Options()
            {
                EnginePath = new Option<string>("--engine-path");
                Distribution = new Option<string>("--distribution");
                TestName = new Option<string>("--test-name");
            }
        }

        public static Command CreateRunGauntletCommand()
        {
            var options = new Options();
            var command = new Command("run-gauntlet-test-from-buildgraph");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunGauntletCommandInstance>(options);
            return command;
        }

        private class RunGauntletCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunGauntletCommandInstance> _logger;

            public RunGauntletCommandInstance(
                ILogger<RunGauntletCommandInstance> logger)
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
