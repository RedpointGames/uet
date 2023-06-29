namespace UET.Commands.Upgrade
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal class UpgradeCommand
    {
        internal class Options
        {
            public Option<string?> Version;
            public Option<bool> DoNotSetAsCurrent;

            public Options()
            {
                Version = new Option<string?>(
                    "--version",
                    description: "The version to install. If not set, installs the latest version.");

                DoNotSetAsCurrent = new Option<bool>(
                    "--do-not-set-as-current",
                    description: "If set, then the version will only be downloaded. It won't be set as the current version to use.");
            }
        }

        public static Command CreateUpgradeCommand(HashSet<Command> globalCommands)
        {
            var options = new Options();
            var command = new Command("upgrade", "Upgrades your version of UET.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpgradeCommandInstance>(
                options,
                services =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            globalCommands.Add(command);
            return command;
        }

        internal class UpgradeCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<UpgradeCommandInstance> _logger;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;

            public UpgradeCommandInstance(
                Options options,
                ILogger<UpgradeCommandInstance> logger,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory)
            {
                _options = options;
                _logger = logger;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version);
                var doNotSetAsCurrent = context.ParseResult.GetValueForOption(_options.DoNotSetAsCurrent);
                return await UpgradeCommandImplementation.PerformUpgradeAsync(
                    _progressFactory,
                    _monitorFactory,
                    _logger,
                    version,
                    doNotSetAsCurrent);
            }
        }
    }
}
