namespace UET.Commands.Upgrade
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProgressMonitor;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal static class UpgradeCommand
    {
        internal sealed class Options
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

        public static ICommandLineBuilder RegisterUpgradeCommand(
            this ICommandLineBuilder rootBuilder,
            HashSet<Command> globalCommands)
        {
            var command = new Command("upgrade", "Upgrades your version of UET.");
            globalCommands.Add(command);
            rootBuilder.AddCommand<UpgradeCommandInstance, Options>(
                _ => command,
                (_, services, _) =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            return rootBuilder;
        }

        internal sealed class UpgradeCommandInstance : ICommandInstance
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

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version);
                var doNotSetAsCurrent = context.ParseResult.GetValueForOption(_options.DoNotSetAsCurrent);
                do
                {
                    try
                    {
                        return await UpgradeCommandImplementation.PerformUpgradeAsync(
                            _progressFactory,
                            _monitorFactory,
                            _logger,
                            version,
                            doNotSetAsCurrent,
                            context.GetCancellationToken()).ConfigureAwait(false);
                    }
                    catch (IOException ex) when (ex.Message.Contains("used by another process", StringComparison.Ordinal))
                    {
                        _logger.LogWarning($"Another UET shim instance is downloading this version, checking if it is ready in another 2 seconds...");
                        await Task.Delay(2000).ConfigureAwait(false);
                        continue;
                    }
                }
                while (true);
            }
        }
    }
}
