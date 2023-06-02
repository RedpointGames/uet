namespace UET.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.MSBuildResolution;
    using Redpoint.OpenGE;
    using Redpoint.OpenGE.Executor;
    using Redpoint.OpenGE.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
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
                services.AddOpenGEExecutor();
                services.AddOpenGEProcessExecution();
                services.AddUETUAT();
                services.AddUETBuildPipeline();
                services.AddUETBuildPipelineExecutorsLocal();
                services.AddUETBuildPipelineExecutorsGitLab();
                services.AddUETWorkspace();
                services.AddUETCore();
                services.AddSingleton<TCommand>();
                services.AddSingleton<ISelfLocation, DefaultSelfLocation>();
                services.AddSingleton<IVersioning, DefaultVersioning>();
                if (extraServices != null)
                {
                    extraServices(services);
                }
                var sp = services.BuildServiceProvider();
                var instance = sp.GetRequiredService<TCommand>();
                var daemon = sp.GetRequiredService<IOpenGEDaemon>();
                try
                {
                    context.ExitCode = await instance.ExecuteAsync(context);
                }
                finally
                {
                    await daemon.StopAsync(CancellationToken.None);
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
