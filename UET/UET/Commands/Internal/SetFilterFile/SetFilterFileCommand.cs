namespace UET.Commands.Internal.SetFilterFile
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class SetFilterFileCommand
    {
        internal class Options
        {
            public Option<string> PackageInclude;
            public Option<string> PackageExclude;
            public Option<string> OutputPath;

            public Options()
            {
                PackageInclude = new Option<string>("--package-include");
                PackageExclude = new Option<string>("--package-exclude");
                OutputPath = new Option<string>("--output-path");
            }
        }

        public static Command CreateSetFilterFileCommand()
        {
            var options = new Options();
            var command = new Command("set-filter-file");
            command.AddAllOptions(options);
            command.AddCommonHandler<SetFilterFileCommandInstance>(options);
            return command;
        }

        private class SetFilterFileCommandInstance : ICommandInstance
        {
            private readonly ILogger<SetFilterFileCommandInstance> _logger;

            public SetFilterFileCommandInstance(
                ILogger<SetFilterFileCommandInstance> logger)
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
