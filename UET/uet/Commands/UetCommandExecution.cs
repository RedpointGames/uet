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
            services.AddUetCore(
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
            // We didn't re-execute into a different version of UET. Invoke the originally requested command.
            return await ExecuteWithApplicationLifecycles(execution).ConfigureAwait(false);
        }
    }
}
