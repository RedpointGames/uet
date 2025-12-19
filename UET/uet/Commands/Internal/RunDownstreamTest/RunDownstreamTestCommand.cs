namespace UET.Commands.Internal.RunDownstreamTest
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class RunDownstreamTestCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<RunDownstreamTestCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("run-downstream-test");
                })
            .Build();

        internal sealed class Options
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

        private sealed class RunDownstreamTestCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunDownstreamTestCommandInstance> _logger;

            public RunDownstreamTestCommandInstance(
                ILogger<RunDownstreamTestCommandInstance> logger)
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
