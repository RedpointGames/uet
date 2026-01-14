namespace UET.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ApplicationLifecycle;
    using Redpoint.AutoDiscovery;
    using Redpoint.CommandLine;
    using Redpoint.Concurrency;
    using Redpoint.CredentialDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.GrpcPipes.Transport.Tcp;
    using Redpoint.KubernetesManager;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.MSBuildResolution;
    using Redpoint.PackageManagement;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Reservation;
    using Redpoint.ServiceControl;
    using Redpoint.Tasks;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet;
    using Redpoint.Uet.Automation;
    using Redpoint.Uet.Automation.TestLogger;
    using Redpoint.Uet.BuildPipeline;
    using Redpoint.Uet.BuildPipeline.Executors.Jenkins;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.BuildPipeline.Providers.Test;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Core.BugReport;
    using Redpoint.Uet.Database;
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.Uat;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using UET.Commands.Cluster;
    using UET.Commands.Upgrade;
    using UET.Services;

    internal class UetCommandExecution
    {
        private static Option<bool> _trace = new Option<bool>(
            "--trace",
            () => Environment.GetEnvironmentVariable("UET_TRACE") == "1",
            "Emit trace level logs to the output.");

        private static Option<bool> _bugReport = new Option<bool>(
            "--bug-report",
            () => Environment.GetEnvironmentVariable("UET_BUG_REPORT") == "1",
            "Collect all logs and generated files into a ZIP file for submission with a bug report.");

        internal static Option<bool> GetTraceOption()
        {
            return _trace;
        }

        internal static Option<bool> GetBugReportOption()
        {
            return _bugReport;
        }

        private static void AddGeneralServices(IServiceCollection services, LogLevel minimumLogLevel, bool permitRunbackLogging, bool bugReporting)
        {
            if (bugReporting)
            {
                services.AddSingleton(BugReportCollector.Instance);
            }
            services.AddAutoDiscovery();
            services.AddPathResolution();
            services.AddMSBuildPathResolution();
            services.AddReservation();
            services.AddProcessExecution();
            services.AddProgressMonitor();
            services.AddTasks();
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                services.AddServiceControl();
            }
            services.AddSdkManagement();
            services.AddGrpcPipes<TcpGrpcPipeFactory>();
            services.AddUefs();
            services.AddPackageManagement();
            services.AddUet();
            services.AddUETAutomation();
            services.AddUETUAT();
            services.AddUETBuildPipeline();
            services.AddUETBuildPipelineExecutorsLocal();
            services.AddUETBuildPipelineExecutorsGitLab();
            services.AddUETBuildPipelineExecutorsJenkins();
            services.AddUetBuildPipelineProvidersPrepare();
            services.AddUETBuildPipelineProvidersTest();
            services.AddUETBuildPipelineProvidersDeployment();
            services.AddUetWorkspace();
            services.AddUETCore(
                minimumLogLevel: minimumLogLevel,
                permitRunbackLogging: permitRunbackLogging,
                bugReportCollector: bugReporting ? BugReportCollector.Instance : null);
            services.AddUetDatabase();
            services.AddCredentialDiscovery();
            services.AddSingleton<ISelfLocation, DefaultSelfLocation>();
            services.AddSingleton<IGitCredentialHelperProvider, DefaultGitCredentialHelperProvider>();
            services.AddUba();

            services.AddSingleton<IRkmVersionProvider, UetRkmVersionProvider>();
            services.AddSingleton<IRkmSelfUpgradeService, UetRkmSelfUpgradeService>();
        }

        private sealed class CommandUetGlobalArgsProvider : IGlobalArgsProvider
        {
            public CommandUetGlobalArgsProvider(string globalArgsString, string[] globalArgsArray)
            {
                GlobalArgsString = globalArgsString;
                GlobalArgsArray = globalArgsArray;
            }

            public string GlobalArgsString { get; }

            public IReadOnlyList<string> GlobalArgsArray { get; }
        }

        public static void AddGlobalRuntimeServices(ICommandLineBuilder<UetGlobalCommandContext> builder, IServiceCollection services, ICommandInvocationContext context)
        {
            AddGeneralServices(
                services,
                minimumLogLevel: context.ParseResult.GetValueForOption(GetTraceOption()) ? LogLevel.Trace : LogLevel.Information,
                permitRunbackLogging: string.Equals(context.ParseResult.CommandResult?.Command?.Name, "ci-build", StringComparison.Ordinal),
                bugReporting: context.ParseResult.GetValueForOption(GetBugReportOption()));

            var globalArgs = new List<string>();
            if (context.ParseResult.GetValueForOption(GetTraceOption()))
            {
                globalArgs.Add("--trace");
            }
            if (context.ParseResult.GetValueForOption(GetBugReportOption()))
            {
                globalArgs.Add("--bug-report");
            }
            services.AddSingleton<IGlobalArgsProvider>(new CommandUetGlobalArgsProvider(string.Join(' ', globalArgs), globalArgs.ToArray()));

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UET_AUTOMATION_LOGGER_PIPE_NAME")) &&
                AutomationLoggerPipe.AllowLoggerPipe)
            {
                // Run commands with an automation logger shim if we don't already have one.
                services.AddSingleton<IApplicationLifecycle>(sp => sp.GetRequiredService<IAutomationLogForwarder>());
            }
        }

        public static void AddGlobalParsingServices(ICommandLineBuilder<UetGlobalCommandContext> builder, IServiceCollection parsingServices)
        {
            AddGeneralServices(
                parsingServices,
                minimumLogLevel: LogLevel.Information,
                permitRunbackLogging: false,
                bugReporting: false);
        }

        private static async Task<int> ExecuteWithApplicationLifecycles(CommandExecution<UetGlobalCommandContext> execution)
        {
            var exitCode = 1;

            // Run the command with all the lifecycles started and stopped around it.
            var logger = execution.ServiceProvider.GetRequiredService<ILogger<UetCommandExecution>>();
            var lifecycles = execution.ServiceProvider.GetServices<IApplicationLifecycle>();
            var startedLifecycles = new List<IApplicationLifecycle>();
            try
            {
                try
                {
                    foreach (var lifecycle in lifecycles)
                    {
                        await lifecycle.StartAsync(execution.CommandInvocationContext.GetCancellationToken()).ConfigureAwait(false);
                        startedLifecycles.Add(lifecycle);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Uncaught exception during application lifecycle startup: {ex}");
                    throw;
                }

                try
                {
                    exitCode = await execution.ExecuteCommandAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Uncaught exception during command execution: {ex}");
                    throw;
                }
            }
            finally
            {
                foreach (var lifecycle in startedLifecycles)
                {
                    try
                    {
                        await lifecycle.StopAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Uncaught exception during application lifecycle shutdown: {ex}");
                    }
                }
            }

            // BuildGraph misses the last line of a command's output if it does
            // not have a final newline, but the .NET console logger does not do
            // this by default. Ensure we see all the output when we're running
            // under BuildGraph.
            if (Environment.GetEnvironmentVariable("UET_RUNNING_UNDER_BUILDGRAPH") == "1")
            {
                Console.WriteLine();
            }

            return exitCode;
        }

        public static async Task<int> ExecuteAsync(CommandExecution<UetGlobalCommandContext> execution)
        {
            var isGlobalCommand = execution.GlobalContext.IsGlobalCommand(execution.Command);

            // If we have a BuildConfig.json file in this folder, and that file specifies a
            // UETVersion, then we must use that version specifically.
            if (!isGlobalCommand && Environment.GetEnvironmentVariable("UET_RUNNING_UNDER_BUILDGRAPH") != "true" &&
                Environment.GetEnvironmentVariable("UET_VERSION_CHECK_COMPLETE") != "true")
            {
                var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
                var currentVersionAttributeValue = RedpointSelfVersion.GetInformationalVersion();
                string? targetVersion = null;
                if (File.Exists(currentBuildConfigPath) && currentVersionAttributeValue != null)
                {
                    try
                    {
                        var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath).ConfigureAwait(false));
                        targetVersion = document!.AsObject()["UETVersion"]!.ToString();
                    }
                    catch
                    {
                    }

                    var services = new ServiceCollection();
                    services.AddUETCore(permitRunbackLogging: execution.GlobalContext.Args.Contains("ci-build", StringComparer.Ordinal));
                    services.AddTasks();
                    services.AddProcessExecution();
                    await using (services.BuildServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
                    {
                        var logger = sp.GetRequiredService<ILogger<Program>>();
                        var processExecutor = sp.GetRequiredService<IProcessExecutor>();

                        var versionRegex = new Regex("^[0-9\\.]+$");
                        if (targetVersion != null && targetVersion != "BleedingEdge" && !versionRegex.IsMatch(targetVersion))
                        {
                            logger.LogError($"The BuildConfig.json file requested version '{targetVersion}', but this isn't a valid version string.");
                            return 1;
                        }

                        if (targetVersion != null && (targetVersion != currentVersionAttributeValue || targetVersion == "BleedingEdge"))
                        {
                            if (Debugger.IsAttached)
                            {
                                logger.LogWarning($"The BuildConfig.json file requested version {targetVersion}, but we are running under a debugger, so this is being ignored.");
                            }
                            else if (currentVersionAttributeValue.EndsWith("-pre", StringComparison.Ordinal))
                            {
                                logger.LogWarning($"The BuildConfig.json file requested version {targetVersion}, but we are running a pre-release or development version of UET, so this is being ignored.");
                            }
                            else
                            {
                                if (targetVersion == "BleedingEdge")
                                {
                                    logger.LogInformation($"The BuildConfig.json file requested the bleeding-edge version of UET, so we need to check what the newest available version is...");
                                }
                                else
                                {
                                    logger.LogInformation($"The BuildConfig.json file requested version {targetVersion}, but we are running {currentVersionAttributeValue}. Obtaining the right version for this build and re-executing the requested command as version {targetVersion}...");
                                }
                                var didInstall = false;
                                var isBleedingEdgeTheSame = false;
                                do
                                {
                                    try
                                    {
                                        var upgradeRootCommand = CommandLineBuilder.NewBuilder(execution.GlobalContext)
                                            .AddCommand<UpgradeCommand>()
                                            .Build("An unofficial tool for Unreal Engine.");

                                        var upgradeArgs = new[] { "upgrade", "--version", targetVersion!, "--do-not-set-as-current" };
                                        if (targetVersion == "BleedingEdge")
                                        {
                                            upgradeArgs = new[] { "upgrade", "--do-not-set-as-current" };
                                        }
                                        var upgradeResult = await upgradeRootCommand.InvokeAsync(upgradeArgs).ConfigureAwait(false);
                                        if (upgradeResult != 0)
                                        {
                                            logger.LogError($"Failed to install the requested UET version {targetVersion}. See above for details.");
                                            return 1;
                                        }

                                        didInstall = true;
                                        if (targetVersion == "BleedingEdge")
                                        {
                                            targetVersion = UpgradeCommandImplementation.LastInstalledVersion!;
                                            if (targetVersion == currentVersionAttributeValue)
                                            {
                                                isBleedingEdgeTheSame = true;
                                            }
                                            else
                                            {
                                                logger.LogInformation($"The bleeding-edge version of UET is {targetVersion}, but we are running {currentVersionAttributeValue}. Re-executing the requested command as version {targetVersion}...");
                                            }
                                        }
                                    }
                                    catch (IOException ex) when (ex.Message.Contains("used by another process", StringComparison.Ordinal))
                                    {
                                        logger.LogWarning($"Another UET instance is downloading {targetVersion}, checking if it is ready in another 2 seconds...");
                                        await Task.Delay(2000).ConfigureAwait(false);
                                        continue;
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, $"Failed to install the requested UET version {targetVersion}. Exception was: {ex.Message}");
                                        return 1;
                                    }
                                    break;
                                } while (true);

                                if (didInstall && !isBleedingEdgeTheSame)
                                {
                                    var cts = new CancellationTokenSource();
                                    Console.CancelKeyPress += (sender, args) =>
                                    {
                                        cts.Cancel();
                                    };

                                    // @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
                                    var nestedExitCode = await processExecutor.ExecuteAsync(
                                        new ProcessSpecification
                                        {
                                            FilePath = UpgradeCommandImplementation.GetAssemblyPathForVersion(targetVersion),
                                            Arguments = execution.GlobalContext.Args.Select(x => new LogicalProcessArgument(x)),
                                            WorkingDirectory = Environment.CurrentDirectory,
                                            EnvironmentVariables = new Dictionary<string, string>
                                            {
                                                { "UET_VERSION_CHECK_COMPLETE", "true" }
                                            }
                                        },
                                        CaptureSpecification.Passthrough,
                                        cts.Token).ConfigureAwait(false);

                                    return nestedExitCode;
                                }
                            }
                        }
                    }
                }
            }

            // Ensure we do not re-use MSBuild processes, because our dotnet executables
            // will often be inside UEFS packages and mounts that might go away at any time.
            Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

            // On macOS, we always want to use the command line tools DEVELOPER_DIR by default,
            // since we'll need to run Git before we potentially have Xcode installed. Also, if
            // we clear out Xcode.app from the Applications folder (because we're using UET to 
            // manage it), then we don't want our command line tools to be broken.
            if (OperatingSystem.IsMacOS())
            {
                if (!Directory.Exists("/Library/Developer/CommandLineTools"))
                {
                    var macosXcodeSelectServices = new ServiceCollection();
                    macosXcodeSelectServices.AddUETCore(permitRunbackLogging: execution.GlobalContext.Args.Contains("ci-build", StringComparer.Ordinal));
                    macosXcodeSelectServices.AddProcessExecution();
                    await using (macosXcodeSelectServices.BuildServiceProvider().AsAsyncDisposable(out var macosXcodeSelectProvider).ConfigureAwait(false))
                    {
                        var macosXcodeProcessExecution = macosXcodeSelectProvider.GetRequiredService<IProcessExecutor>();
                        var macosXcodeSelectLogger = macosXcodeSelectProvider.GetRequiredService<ILogger<Program>>();

                        macosXcodeSelectLogger.LogInformation("Installing macOS Command Line Tools...");
                        await macosXcodeProcessExecution.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = "/usr/bin/sudo",
                                Arguments = new LogicalProcessArgument[]
                                {
                                    "xcode-select",
                                    "--install"
                                }
                            },
                            CaptureSpecification.Passthrough,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }

                Environment.SetEnvironmentVariable("DEVELOPER_DIR", "/Library/Developer/CommandLineTools");
            }

            // We didn't re-execute into a different version of UET. Invoke the originally requested command.
            return await ExecuteWithApplicationLifecycles(execution).ConfigureAwait(false);
        }
    }
}
