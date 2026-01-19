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

    // Prevent all logging.
    CoreServiceExtensions.SuppressAllLogging = true;
}

// Construct the root command. We have to do this to see what command the user
// is invoking, to make sure we don't do the BuildConfig.json-based version switch
// if the user is invoking a "global" command.
var globalCommandContext = new UetGlobalCommandContext();
var rootCommand = CommandLineBuilder.NewBuilder(globalCommandContext)
    .AddGlobalOption(UetCommandExecution.GetTraceOption())
    .AddGlobalOption(UetCommandExecution.GetBugReportOption())
    .AddGlobalRuntimeServices(UetCommandExecution.AddGlobalRuntimeServices)
    .AddGlobalParsingServices(UetCommandExecution.AddGlobalParsingServices)
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
    args = new[] { "internal", implicitCommand }.Concat(args).ToArray();
}

//
// @warning: The logic below must NOT be moved as part of command execution, as it instantiates service providers
// temporarily. Those service providers will open the runback file stream for the lifetime of those service
// providers, which can't happen if the main execution service provider has already been built.
//
// In addition, we don't want to re-use the same service provider as the main execution service provider, because
// this would result in incorrect behaviour for when we construct and execute the upgrade command based on the
// BuildConfig.json file.
//

// Parse the command line so we can inspect it.
var parseResult = rootCommand.Parse(args);
var isGlobalCommand = globalCommandContext.IsGlobalCommand(parseResult.CommandResult.Command);

// If we have a BuildConfig.json file in this folder, and that file specifies a
// UETVersion, then we must use that version specifically.
if (!isGlobalCommand && Environment.GetEnvironmentVariable("UET_RUNNING_UNDER_BUILDGRAPH") != "true" &&
    Environment.GetEnvironmentVariable("UET_VERSION_CHECK_COMPLETE") != "true")
{
    var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
    var currentVersionAttributeValue = RedpointSelfVersion.GetInformationalVersion();
    string? targetVersion = null;
    if (File.Exists(currentBuildConfigPath) && currentVersionAttributeValue != null)
    {
        try
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath).ConfigureAwait(false));
            targetVersion = document!.AsObject()["UETVersion"]!.ToString();
        }
        catch
        {
        }

        var services = new ServiceCollection();
        services.AddUetCore(permitRunbackLogging: args.Contains("ci-build", StringComparer.Ordinal));
        services.AddTasks();
        services.AddProcessExecution();
        await using (services.BuildServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            var processExecutor = sp.GetRequiredService<IProcessExecutor>();

            var versionRegex = new Regex("^[0-9\\.]+$");
            if (targetVersion != null && targetVersion != "BleedingEdge" && !versionRegex.IsMatch(targetVersion))
            {
                logger.LogError($"The BuildConfig.json file requested version '{targetVersion}', but this isn't a valid version string.");
                return 1;
            }

            if (targetVersion != null && (targetVersion != currentVersionAttributeValue || targetVersion == "BleedingEdge"))
            {
                if (Debugger.IsAttached)
                {
                    logger.LogWarning($"The BuildConfig.json file requested version {targetVersion}, but we are running under a debugger, so this is being ignored.");
                }
                else if (currentVersionAttributeValue.EndsWith("-pre", StringComparison.Ordinal))
                {
                    logger.LogWarning($"The BuildConfig.json file requested version {targetVersion}, but we are running a pre-release or development version of UET, so this is being ignored.");
                }
                else
                {
                    if (targetVersion == "BleedingEdge")
                    {
                        logger.LogInformation($"The BuildConfig.json file requested the bleeding-edge version of UET, so we need to check what the newest available version is...");
                    }
                    else
                    {
                        logger.LogInformation($"The BuildConfig.json file requested version {targetVersion}, but we are running {currentVersionAttributeValue}. Obtaining the right version for this build and re-executing the requested command as version {targetVersion}...");
                    }
                    var didInstall = false;
                    var isBleedingEdgeTheSame = false;
                    do
                    {
                        try
                        {
                            var upgradeRootCommand = CommandLineBuilder.NewBuilder(globalCommandContext)
                                .AddGlobalOption(UetCommandExecution.GetTraceOption())
                                .AddGlobalOption(UetCommandExecution.GetBugReportOption())
                                .AddGlobalRuntimeServices(UetCommandExecution.AddGlobalRuntimeServices)
                                .AddGlobalParsingServices(UetCommandExecution.AddGlobalParsingServices)
                                .AddCommand<UpgradeCommand>()
                                .Build("An unofficial tool for Unreal Engine.");

                            var upgradeArgs = new[] { "upgrade", "--version", targetVersion!, "--do-not-set-as-current" };
                            if (targetVersion == "BleedingEdge")
                            {
                                upgradeArgs = new[] { "upgrade", "--do-not-set-as-current" };
                            }
                            var upgradeResult = await upgradeRootCommand.InvokeAsync(upgradeArgs).ConfigureAwait(false);
                            if (upgradeResult != 0)
                            {
                                logger.LogError($"Failed to install the requested UET version {targetVersion}. See above for details.");
                                return 1;
                            }

                            didInstall = true;
                            if (targetVersion == "BleedingEdge")
                            {
                                targetVersion = UpgradeCommandImplementation.LastInstalledVersion!;
                                if (targetVersion == currentVersionAttributeValue)
                                {
                                    isBleedingEdgeTheSame = true;
                                }
                                else
                                {
                                    logger.LogInformation($"The bleeding-edge version of UET is {targetVersion}, but we are running {currentVersionAttributeValue}. Re-executing the requested command as version {targetVersion}...");
                                }
                            }
                        }
                        catch (IOException ex) when (ex.Message.Contains("used by another process", StringComparison.Ordinal))
                        {
                            logger.LogWarning($"Another UET instance is downloading {targetVersion}, checking if it is ready in another 2 seconds...");
                            await Task.Delay(2000).ConfigureAwait(false);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Failed to install the requested UET version {targetVersion}. Exception was: {ex.Message}");
                            return 1;
                        }
                        break;
                    } while (true);

                    if (didInstall && !isBleedingEdgeTheSame)
                    {
                        var cts = new CancellationTokenSource();
                        Console.CancelKeyPress += (sender, args) =>
                        {
                            cts.Cancel();
                        };

                        // @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
                        var nestedExitCode = await processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = UpgradeCommandImplementation.GetAssemblyPathForVersion(targetVersion),
                                Arguments = args.Select(x => new LogicalProcessArgument(x)),
                                WorkingDirectory = Environment.CurrentDirectory,
                                EnvironmentVariables = new Dictionary<string, string>
                                {
                                    { "UET_VERSION_CHECK_COMPLETE", "true" }
                                }
                            },
                            CaptureSpecification.Passthrough,
                            cts.Token).ConfigureAwait(false);

                        return nestedExitCode;
                    }
                }
            }
        }
    }
}

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

// On macOS, we always want to use the command line tools DEVELOPER_DIR by default,
// since we'll need to run Git before we potentially have Xcode installed. Also, if
// we clear out Xcode.app from the Applications folder (because we're using UET to 
// manage it), then we don't want our command line tools to be broken.
if (OperatingSystem.IsMacOS())
{
    if (!Directory.Exists("/Library/Developer/CommandLineTools"))
    {
        var macosXcodeSelectServices = new ServiceCollection();
        macosXcodeSelectServices.AddUetCore(permitRunbackLogging: args.Contains("ci-build", StringComparer.Ordinal));
        macosXcodeSelectServices.AddProcessExecution();
        await using (macosXcodeSelectServices.BuildServiceProvider().AsAsyncDisposable(out var macosXcodeSelectProvider).ConfigureAwait(false))
        {
            var macosXcodeProcessExecution = macosXcodeSelectProvider.GetRequiredService<IProcessExecutor>();
            var macosXcodeSelectLogger = macosXcodeSelectProvider.GetRequiredService<ILogger<Program>>();

            macosXcodeSelectLogger.LogInformation("Installing macOS Command Line Tools...");
            await macosXcodeProcessExecution.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/sudo",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "xcode-select",
                        "--install"
                    }
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    Environment.SetEnvironmentVariable("DEVELOPER_DIR", "/Library/Developer/CommandLineTools");
}

// Execute the command. InvokeAsync may result in UET upgrading as per SetGlobalExecutionHandler.
// @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
var exitCode = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
BugReportCollector.DisposeIfInitialized();
await Console.Out.FlushAsync().ConfigureAwait(false);
await Console.Error.FlushAsync().ConfigureAwait(false);
Environment.Exit(exitCode);
throw new BadImageFormatException();