using Microsoft.Extensions.DependencyInjection;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using Redpoint.UET.BuildPipeline;
using Redpoint.UET.Core;
using Redpoint.UET.Workspace;
using System.CommandLine;
using UET.Commands;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var services = new ServiceCollection();
services.AddPathResolution();
services.AddProcessExecution();
services.AddUETBuildPipeline();
services.AddUETWorkspace();
services.AddUETCore();

var rootCommand = new RootCommand("Build runner for Unreal Engine.");
rootCommand.AddOption(GlobalOptions.RepositoryRoot);
rootCommand.AddCommand(BuildCommand.CreateBuildCommand(services));
return await rootCommand.InvokeAsync(args);
