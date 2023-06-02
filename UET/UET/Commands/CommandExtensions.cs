namespace UET.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.MSBuildResolution;
    using Redpoint.OpenGE;
    using Redpoint.OpenGE.Executor;
    using Redpoint.OpenGE.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.UET.BuildPipeline;
    using Redpoint.UET.BuildPipeline.Executors.Local;
    using Redpoint.UET.Core;
    using Redpoint.UET.UAT;
    using Redpoint.UET.Workspace;
    using System.CommandLine;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using UET.Services;

    internal static class CommandExtensions
    {
        internal static Option<bool> _trace = new Option<bool>("--trace", "Emit trace level logs to the output.");

        internal static Option<bool> GetTraceOption()
        {
            return _trace;
        }

        internal static void AddAllOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(this Command command, TOptions options)
        {
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

        internal static void AddCommonHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this Command command, object options, Action<IServiceCollection>? extraServices = null) where TCommand : class, ICommandInstance
        {
            command.SetHandler(async (context) =>
            {
                var services = new ServiceCollection();
                services.AddSingleton(sp => context);
                services.AddSingleton(options.GetType(), sp => options);
                services.AddPathResolution();
                services.AddMSBuildPathResolution();
                services.AddProcessExecution();
                services.AddProgressMonitor();
                services.AddOpenGEExecutor();
                services.AddOpenGEProcessExecution();
                services.AddUETUAT();
                services.AddUETBuildPipeline();
                services.AddUETBuildPipelineExecutorsLocal();
                services.AddUETBuildPipelineExecutorsGitLab();
                services.AddUETWorkspace();
                services.AddUETCore(minimumLogLevel: context.ParseResult.GetValueForOption(GetTraceOption()) ? LogLevel.Trace : LogLevel.Information);
                services.AddSingleton<TCommand>();
                services.AddSingleton<ISelfLocation, DefaultSelfLocation>();
                services.AddSingleton<IPluginVersioning, DefaultPluginVersioning>();
                if (extraServices != null)
                {
                    extraServices(services);
                }
                var sp = services.BuildServiceProvider();
                var instance = sp.GetRequiredService<TCommand>();
                var daemon = sp.GetRequiredService<IOpenGEDaemon>();

                // Run the command with an XGE shim if we don't already have one.
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UET_XGE_SHIM_PIPE_NAME")))
                {
                    await daemon.StartAsync(context.GetCancellationToken());
                    try
                    {
                        context.ExitCode = await instance.ExecuteAsync(context);
                    }
                    finally
                    {
                        await daemon.StopAsync();
                    }
                }
                else
                {
                    context.ExitCode = await instance.ExecuteAsync(context);
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
