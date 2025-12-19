using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.Concurrency;
using Redpoint.ProcessExecution;
using Redpoint.Tasks;
using Redpoint.Uet.Automation.TestLogger;
using Redpoint.Uet.Core;
using Redpoint.Uet.Core.BugReport;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UET.Commands;
using UET.Commands.Android;
using UET.Commands.AppleCert;
using UET.Commands.Build;
using UET.Commands.Cluster;
using UET.Commands.CMake;
using UET.Commands.Config;
using UET.Commands.Format;
using UET.Commands.Generate;
using UET.Commands.InstallSdks;
using UET.Commands.Internal;
using UET.Commands.List;
using UET.Commands.New;
using UET.Commands.Storage;
using UET.Commands.Test;
using UET.Commands.Transfer;
using UET.Commands.Uefs;
using UET.Commands.Upgrade;

if (Environment.GetEnvironmentVariable("CI") == "true")
{
    Crayon.Output.Enable();
}

if (args.Any(x => string.Equals(x, "git-credential-helper", StringComparison.OrdinalIgnoreCase)))
{
    // Do not allow the automation logger pipe to be used if it looks like we're invoking the Git credential helper.
    AutomationLoggerPipe.AllowLoggerPipe = false;
}

// Construct the root command. We have to do this to see what command the user
// is invoking, to make sure we don't do the BuildConfig.json-based version switch
// if the user is invoking a "global" command.
var globalCommandContext = new UetGlobalCommandContext(args);
var rootCommand = CommandLineBuilder.NewBuilder(globalCommandContext)
    .AddGlobalOption(UetCommandExecution.GetTraceOption())
    .AddGlobalOption(UetCommandExecution.GetBugReportOption())
    .AddGlobalRuntimeServices(UetCommandExecution.AddGlobalRuntimeServices)
    .AddCommand<BuildCommand>()
    .AddCommand<TestCommand>()
    .AddCommand<GenerateCommand>()
    .AddCommand<NewCommand>()
    .AddCommand<RunCommand>()
    .AddCommand<ConfigCommand>()
    .AddCommand<FormatCommand>()
    .AddCommand<ListCommand>()
    .AddCommand<InstallSdksCommand>()
    .AddCommand<UpgradeCommand>()
    .AddCommand<StorageCommand>()
    .AddCommand<UefsCommand>()
    .AddCommand<TransferCommand>()
    .AddCommand<AppleCertCommand>()
    .AddCommand<AndroidCommand>()
    .AddCommand<CMakeCommand>()
    .AddCommand<ClusterCommand>()
    .AddCommand<InternalCommand>()
    .SetGlobalExecutionHandler(UetCommandExecution.ExecuteAsync)
    .Build("An unofficial tool for Unreal Engine.");

// If we have an implicit command variable, this is an internal command where we can't specify arguments directly.
var implicitCommand = Environment.GetEnvironmentVariable("UET_IMPLICIT_COMMAND");
if (!string.IsNullOrWhiteSpace(implicitCommand))
{
    // Clear it for any downstream processes we might start.
    Environment.SetEnvironmentVariable("UET_IMPLICIT_COMMAND", null);

    // Prepend to args.
    globalCommandContext.Args = new[] { "internal", implicitCommand }.Concat(globalCommandContext.Args).ToArray();
}

// Execute the command. InvokeAsync may result in UET upgrading as per SetGlobalExecutionHandler.
// @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
var exitCode = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
BugReportCollector.DisposeIfInitialized();
await Console.Out.FlushAsync().ConfigureAwait(false);
await Console.Error.FlushAsync().ConfigureAwait(false);
Environment.Exit(exitCode);
throw new BadImageFormatException();