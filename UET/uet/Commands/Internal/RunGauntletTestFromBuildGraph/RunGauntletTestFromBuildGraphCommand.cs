namespace UET.Commands.Internal.RunGauntletTestFromBuildGraph
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class RunGauntletTestFromBuildGraphCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<RunGauntletCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("run-gauntlet-test-from-buildgraph");
                })
            .Build();

        internal sealed class Options
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

        private sealed class RunGauntletCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunGauntletCommandInstance> _logger;

            public RunGauntletCommandInstance(
                ILogger<RunGauntletCommandInstance> logger)
            {
                _logger = logger;
            }

            public Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                _logger.LogError("Not yet implemented.");
                return Task.FromResult(1);
            }
        }
    }
}
