namespace UET.Commands
{
    using Redpoint.AutoDiscovery;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Tasks;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Reservation;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet.BuildPipeline;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.Uet.BuildPipeline.Executors.Jenkins;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.Uat;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.BuildPipeline.Providers.Test;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment;
    using System.CommandLine;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using UET.Services;
    using Redpoint.Uet.Automation;
    using Redpoint.ApplicationLifecycle;
    using Redpoint.Uet.Automation.TestLogger;
    using Redpoint.GrpcPipes;
    using Redpoint.ServiceControl;
    using Redpoint.CredentialDiscovery;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes.Transport.Tcp;
    using Redpoint.PackageManagement;
    using Redpoint.Uet.Core.BugReport;

    internal static class CommandExtensions
    {
        internal static Option<bool> _trace = new Option<bool>(
            "--trace",
            () => Environment.GetEnvironmentVariable("UET_TRACE") == "1",
            "Emit trace level logs to the output.");

        internal static Option<bool> _bugReport = new Option<bool>(
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
            services.AddCredentialDiscovery();
            services.AddSingleton<ISelfLocation, DefaultSelfLocation>();
            services.AddSingleton<IReleaseVersioning, DefaultReleaseVersioning>();
            services.AddUba();
        }

        internal static void AddServicedOptionsHandler<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions
            >(this Command command, Action<IServiceCollection>? extraServices = null, Action<IServiceCollection>? extraParsingServices = null) where TCommand : class, ICommandInstance where TOptions : class
        {
            // We need a service provider for distribution option parsing, omitting services that are post-parsing specific.
            var parsingServices = new ServiceCollection();
            AddGeneralServices(parsingServices, LogLevel.Information, false, false);
            parsingServices.AddTransient<TOptions, TOptions>();
            if (extraParsingServices != null)
            {
                extraParsingServices(parsingServices);
            }
            // @note: This service provider MUST NOT be disposed, as references are held to it by the
            // command arguments and options set up in the Options object, which exists beyond the
            // lifetime of AddServicedOptionsHandler.
            var minimalServiceProvider = parsingServices.BuildServiceProvider();

            // Get the options instance from the minimal service provider.
            var options = minimalServiceProvider.GetRequiredService<TOptions>();
            command.AddAllOptions(options);
            command.AddCommonHandler<TCommand>(options, extraServices, extraParsingServices);
        }

        internal static void AddAllOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(this Command command, TOptions options)
        {
            foreach (var argument in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.PropertyType.IsAssignableTo(typeof(Argument)))
                .Select(x => (Argument)x.GetValue(options)!))
            {
                command.AddArgument(argument);
            }

            foreach (var argument in typeof(TOptions).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.FieldType.IsAssignableTo(typeof(Argument)))
                .Select(x => (Argument)x.GetValue(options)!))
            {
                command.AddArgument(argument);
            }

            foreach (var option in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.PropertyType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }

            foreach (var option in typeof(TOptions).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.FieldType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }
        }

        private sealed class CommandUETGlobalArgsProvider : IGlobalArgsProvider
        {
            public CommandUETGlobalArgsProvider(string globalArgsString, string[] globalArgsArray)
            {
                GlobalArgsString = globalArgsString;
                GlobalArgsArray = globalArgsArray;
            }

            public string GlobalArgsString { get; }

            public IReadOnlyList<string> GlobalArgsArray { get; }
        }

        internal static void AddCommonHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this Command command, object options, Action<IServiceCollection>? extraServices = null, Action<IServiceCollection>? extraParsingServices = null) where TCommand : class, ICommandInstance
        {
            command.SetHandler(async (context) =>
            {
                var services = new ServiceCollection();
                services.AddSingleton(sp => context);
                services.AddSingleton(options.GetType(), sp => options);
                AddGeneralServices(
                    services,
                    minimumLogLevel: context.ParseResult.GetValueForOption(GetTraceOption()) ? LogLevel.Trace : LogLevel.Information,
                    permitRunbackLogging: string.Equals(context.ParseResult.CommandResult?.Command?.Name, "ci-build", StringComparison.Ordinal),
                    bugReporting: context.ParseResult.GetValueForOption(GetBugReportOption()));
                services.AddSingleton<TCommand>();
                var globalArgs = new List<string>();
                if (context.ParseResult.GetValueForOption(GetTraceOption()))
                {
                    globalArgs.Add("--trace");
                }
                if (context.ParseResult.GetValueForOption(GetBugReportOption()))
                {
                    globalArgs.Add("--bug-report");
                }
                services.AddSingleton<IGlobalArgsProvider>(new CommandUETGlobalArgsProvider(string.Join(' ', globalArgs), globalArgs.ToArray()));
                if (extraServices != null)
                {
                    extraServices(services);
                }
                if (extraParsingServices != null)
                {
                    extraParsingServices(services);
                }
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UET_AUTOMATION_LOGGER_PIPE_NAME")))
                {
                    // Run commands with an automation logger shim if we don't already have one.
                    services.AddSingleton<IApplicationLifecycle>(sp => sp.GetRequiredService<IAutomationLogForwarder>());
                }
                await using (services.BuildServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
                {
                    var instance = sp.GetRequiredService<TCommand>();

                    // Run the command with all the lifecycles started and stopped around it.
                    var logger = sp.GetRequiredService<ILogger<TCommand>>();
                    var lifecycles = sp.GetServices<IApplicationLifecycle>();
                    var startedLifecycles = new List<IApplicationLifecycle>();
                    try
                    {
                        try
                        {
                            foreach (var lifecycle in lifecycles)
                            {
                                await lifecycle.StartAsync(context.GetCancellationToken()).ConfigureAwait(false);
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
                            context.ExitCode = await instance.ExecuteAsync(context).ConfigureAwait(false);
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
                }
            });
        }
    }
}
