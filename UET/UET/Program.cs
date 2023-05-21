using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using Redpoint.UET.BuildPipeline;
using Redpoint.UET.Core;
using Redpoint.UET.Workspace;
using System.CommandLine;
using UET.Commands;
using UET.Services;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var services = new ServiceCollection();
services.AddPathResolution();
services.AddProcessExecution();
services.AddUETBuildPipeline();
services.AddUETWorkspace();
services.AddUETCore();
services.AddSingleton<IPathProvider, DefaultPathProvider>();
services.AddSingleton<IBuildConfigProvider, DefaultBuildConfigProvider>();

var rootCommand = new RootCommand("Build runner for Unreal Engine.");
rootCommand.AddOption(GlobalOptions.RepositoryRoot);
rootCommand.AddCommand(BuildCommand.CreateBuildCommand(services));
return await rootCommand.InvokeAsync(args);
