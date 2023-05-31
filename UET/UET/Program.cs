using System.CommandLine;
using UET.Commands.Build;
using UET.Commands.Internal;
using UET.Commands.TestPackaged;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var rootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
rootCommand.AddCommand(BuildCommand.CreateBuildCommand());
rootCommand.AddCommand(TestPackagedCommand.CreateTestPackagedCommand());
rootCommand.AddCommand(InternalCommand.CreateInternalCommand());
return await rootCommand.InvokeAsync(args);
