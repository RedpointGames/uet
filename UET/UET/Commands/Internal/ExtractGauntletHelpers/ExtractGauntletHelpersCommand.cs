namespace UET.Commands.Internal.ExtractGauntletHelpers
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class ExtractGauntletHelpersCommand
    {
        internal class Options
        {
            public Option<string> Path;

            public Options()
            {
                Path = new Option<string>("--path");
            }
        }

        public static Command CreateExtractGauntletHelpersCommand()
        {
            var options = new Options();
            var command = new Command("extract-gauntlet-helpers");
            command.AddAllOptions(options);
            command.AddCommonHandler<ExtractGauntletHelpersCommandInstance>(options);
            return command;
        }

        private class ExtractGauntletHelpersCommandInstance : ICommandInstance
        {
            private readonly ILogger<ExtractGauntletHelpersCommandInstance> _logger;

            public ExtractGauntletHelpersCommandInstance(
                ILogger<ExtractGauntletHelpersCommandInstance> logger)
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
