namespace UET.Commands.Internal.RunDownstreamTest
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class RunDownstreamTestCommand
    {
        internal class Options
        {
            public Option<string> DownstreamTest;
            public Option<string> EnginePath;
            public Option<string> Distribution;
            public Option<string> PackagedPluginPath;

            public Options()
            {
                DownstreamTest = new Option<string>("--downstream-test");
                EnginePath = new Option<string>("--engine-path");
                Distribution = new Option<string>("--distribution");
                PackagedPluginPath = new Option<string>("--packaged-plugin-path");
            }
        }

        public static Command CreateRunDownstreamTestCommand()
        {
            var options = new Options();
            var command = new Command("run-downstream-test");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunDownstreamTestCommandInstance>(options);
            return command;
        }

        private class RunDownstreamTestCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunDownstreamTestCommandInstance> _logger;

            public RunDownstreamTestCommandInstance(
                ILogger<RunDownstreamTestCommandInstance> logger)
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
