namespace UET.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Reservation;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet.BuildPipeline;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
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
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.Uet.OpenGE;
    using Redpoint.OpenGE.Component.Dispatcher;
    using Redpoint.OpenGE.Component.Worker;
    using Redpoint.OpenGE.Agent;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;

    internal static class CommandExtensions
    {
        internal static Option<bool> _trace = new Option<bool>(
            "--trace",
            () => Environment.GetEnvironmentVariable("UET_TRACE") == "1",
            "Emit trace level logs to the output.");

        internal static Option<bool> GetTraceOption()
        {
            return _trace;
        }

        private static void AddGeneralServices(IServiceCollection services, LogLevel minimumLogLevel)
        {
            services.AddPathResolution();
            services.AddMSBuildPathResolution();
            services.AddReservation();
            services.AddProcessExecution();
            services.AddProgressMonitor();
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                services.AddServiceControl();
            }
            services.AddOpenGEAgent();
            services.AddOpenGEComponentDispatcher();
            services.AddOpenGEComponentWorker();
            services.AddOpenGEProcessExecution();
            services.AddOpenGEComponentPreprocessorCache();
            services.AddSdkManagement();
            services.AddGrpcPipes();
            services.AddUefs();
            services.AddUETAutomation();
            services.AddUETUAT();
            services.AddUETBuildPipeline();
            services.AddUETBuildPipelineExecutorsLocal();
            services.AddUETBuildPipelineExecutorsGitLab();
            services.AddUetBuildPipelineProvidersPrepare();
            services.AddUETBuildPipelineProvidersTest();
            services.AddUETBuildPipelineProvidersDeployment();
            services.AddUETWorkspace();
            services.AddUETCore(minimumLogLevel: minimumLogLevel);
            services.AddCredentialDiscovery();
            services.AddSingleton<ISelfLocation, DefaultSelfLocation>();
            services.AddSingleton<IPluginVersioning, DefaultPluginVersioning>();
        }

        internal static void AddServicedOptionsHandler<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions
            >(this Command command, Action<IServiceCollection>? extraServices = null) where TCommand : class, ICommandInstance where TOptions : class
        {
            // We need a service provider for distribution option parsing, omitting services that are post-parsing specific.
            var parsingServices = new ServiceCollection();
            AddGeneralServices(parsingServices, LogLevel.Information);
            parsingServices.AddTransient<TOptions, TOptions>();
            var minimalServiceProvider = parsingServices.BuildServiceProvider();

            // Get the options instance from the minimal service provider.
            var options = minimalServiceProvider.GetRequiredService<TOptions>();
            command.AddAllOptions(options);
            command.AddCommonHandler<TCommand>(options, extraServices);
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

        private class CommandUETGlobalArgsProvider : IGlobalArgsProvider
        {
            public CommandUETGlobalArgsProvider(string globalArgsString, string[] globalArgsArray)
            {
                GlobalArgsString = globalArgsString;
                GlobalArgsArray = globalArgsArray;
            }

            public string GlobalArgsString { get; }

            public string[] GlobalArgsArray { get; }
        }

        internal static void AddCommonHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this Command command, object options, Action<IServiceCollection>? extraServices = null) where TCommand : class, ICommandInstance
        {
            command.SetHandler(async (context) =>
            {
                var services = new ServiceCollection();
                services.AddSingleton(sp => context);
                services.AddSingleton(options.GetType(), sp => options);
                AddGeneralServices(services, minimumLogLevel: context.ParseResult.GetValueForOption(GetTraceOption()) ? LogLevel.Trace : LogLevel.Information);
                services.AddSingleton<TCommand>();
                if (context.ParseResult.GetValueForOption(GetTraceOption()))
                {
                    services.AddSingleton<IGlobalArgsProvider>(new CommandUETGlobalArgsProvider("--trace", new[] { "--trace" }));
                }
                else
                {
                    services.AddSingleton<IGlobalArgsProvider>(new CommandUETGlobalArgsProvider(string.Empty, new string[0]));
                }
                if (extraServices != null)
                {
                    extraServices(services);
                }
                services.AddSingleton<IOpenGEProvider, DefaultOpenGEProvider>();
                services.AddSingleton<IApplicationLifecycle>(sp => sp.GetRequiredService<IOpenGEProvider>());
                services.AddSingleton<IPreprocessorCacheAccessor>(sp => sp.GetRequiredService<IOpenGEProvider>());
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UET_AUTOMATION_LOGGER_PIPE_NAME")))
                {
                    // Run commands with an automation logger shim if we don't already have one.
                    services.AddSingleton<IApplicationLifecycle>(sp => sp.GetRequiredService<IAutomationLogForwarder>());
                }
                var sp = services.BuildServiceProvider();
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
                            await lifecycle.StartAsync(context.GetCancellationToken());
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
                        context.ExitCode = await instance.ExecuteAsync(context);
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
                            await lifecycle.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Uncaught exception during application lifecycle shutdown: {ex}");
                            throw;
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
            });
        }
    }
}
