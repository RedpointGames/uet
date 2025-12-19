namespace UET.Commands.Upgrade
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uet.CommonPaths;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal sealed class UpgradeCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<UpgradeCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("upgrade", "Upgrades your version of UET.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<string?> Version;
            public Option<bool> DoNotSetAsCurrent;
            public Option<bool> WaitForNetwork;
            public Option<bool> Then;
            public Argument<string[]> ThenArgs;

            public Options()
            {
                Version = new Option<string?>(
                    "--version",
                    description: "The version to install. If not set, installs the latest version.");

                DoNotSetAsCurrent = new Option<bool>(
                    "--do-not-set-as-current",
                    description: "If set, then the version will only be downloaded. It won't be set as the current version to use.");

                WaitForNetwork = new Option<bool>(
                    "--wait-for-network",
                    description: "If set, UET will retry downloading from GitHub until a network connection is available.");

                Then = new Option<bool>(
                    "--then",
                    description: "If set, the additional arguments passed to the upgrade command will be the command to invoke on the upgraded version.");

                ThenArgs = new Argument<string[]>(
                    "then-args",
                    description: "If --then has been passed, the command to invoke on the upgraded version.");
            }
        }

        internal sealed class UpgradeCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<UpgradeCommandInstance> _logger;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IProcessExecutor _processExecutor;

            public UpgradeCommandInstance(
                Options options,
                ILogger<UpgradeCommandInstance> logger,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                IProcessExecutor processExecutor)
            {
                _options = options;
                _logger = logger;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _processExecutor = processExecutor;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version);
                var doNotSetAsCurrent = context.ParseResult.GetValueForOption(_options.DoNotSetAsCurrent);
                var waitForNetwork = context.ParseResult.GetValueForOption(_options.WaitForNetwork);
                var delaySeconds = 1;
                var exitCode = -1;
                do
                {
                    try
                    {
                        exitCode = (await UpgradeCommandImplementation.PerformUpgradeAsync(
                            _progressFactory,
                            _monitorFactory,
                            _logger,
                            version,
                            doNotSetAsCurrent,
                            waitForNetwork,
                            context.GetCancellationToken()).ConfigureAwait(false)).ExitCode;
                        break;
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                    {
                        _logger.LogWarning($"Gateway timeout while attempting to contact GitHub. This operation will be retried in {delaySeconds} seconds...");
                        await Task.Delay(delaySeconds * 1000, context.GetCancellationToken()).ConfigureAwait(false);
                        delaySeconds *= 2;
                        if (delaySeconds > 3600 /* 1 hour */)
                        {
                            delaySeconds = 3600;
                        }
                        continue;
                    }
                    catch (IOException ex) when (ex.Message.Contains("used by another process", StringComparison.Ordinal))
                    {
                        _logger.LogWarning($"Another UET shim instance is downloading this version, checking if it is ready in another 2 seconds...");
                        await Task.Delay(2000, context.GetCancellationToken()).ConfigureAwait(false);
                        continue;
                    }
                }
                while (true);

                if (exitCode != 0)
                {
                    return exitCode;
                }

                var then = context.ParseResult.GetValueForOption(_options.Then);
                var thenArgs = context.ParseResult.GetValueForArgument(_options.ThenArgs) ?? [];
                if (then)
                {
                    _logger.LogInformation($"Invoking 'uet {string.Join(" ", thenArgs)}' with upgraded version...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(UetPaths.UetRootPath, "Current", OperatingSystem.IsWindows() ? "uet.exe" : "uet"),
                            Arguments = thenArgs.Select(x => new LogicalProcessArgument(x)),
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                }

                return exitCode;
            }
        }
    }
}
