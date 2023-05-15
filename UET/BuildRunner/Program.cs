using BuildRunner.BuildGraph;
using BuildRunner.Commands;
using BuildRunner.Services;
using BuildRunner.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Redpoint.ProcessExecution;
using System.CommandLine;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var services = new ServiceCollection();
services.AddSingleton<IPathProvider, DefaultPathProvider>();
services.AddSingleton<IBuildConfigProvider, DefaultBuildConfigProvider>();
services.AddSingleton<IStringUtilities, DefaultStringUtilities>();
services.AddSingleton<IBuildStabilityIdProvider, DefaultBuildStabilityIdProvider>();
services.AddSingleton<IWorkspaceProvider, DefaultWorkspaceProvider>();
services.AddSingleton<IBuildGraphExecutor, DefaultBuildGraphExecutor>();
services.AddExecutors();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddConsoleFormatter<SimpleBuildConsoleFormatter, SimpleConsoleFormatterOptions>(options =>
    {
        options.ColorBehavior = LoggerColorBehavior.Default;
        options.SingleLine = true;
        options.IncludeScopes = false;
        options.TimestampFormat = "HH:mm:ss ";
    });
    builder.AddConsole(options =>
    {
        options.FormatterName = "simple-build";
    });
});

var rootCommand = new RootCommand("Build runner for Unreal Engine.");
rootCommand.AddOption(GlobalOptions.RepositoryRoot);
rootCommand.AddCommand(BuildCommand.CreateBuildCommand(services));
return await rootCommand.InvokeAsync(args);
