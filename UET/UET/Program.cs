using Microsoft.Extensions.DependencyInjection;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using Redpoint.UET.BuildPipeline;
using Redpoint.UET.Core;
using Redpoint.UET.Workspace;
using System.CommandLine;
using UET.Commands.Build;
using UET.Commands.TestPackaged;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var rootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
rootCommand.AddCommand(BuildCommand.CreateBuildCommand());
rootCommand.AddCommand(TestPackagedCommand.CreateTestPackagedCommand());
return await rootCommand.InvokeAsync(args);
