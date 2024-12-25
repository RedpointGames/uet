﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.Concurrency;
using Redpoint.ProcessExecution;
using Redpoint.Tasks;
using Redpoint.Uet.Core;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UET.Commands.AppleCert;
using UET.Commands.Build;
using UET.Commands.CMake;
using UET.Commands.Config;
using UET.Commands.Format;
using UET.Commands.Generate;
using UET.Commands.InstallSdks;
using UET.Commands.Internal;
using UET.Commands.List;
using UET.Commands.Storage;
using UET.Commands.Test;
using UET.Commands.Transfer;
using UET.Commands.Uefs;
using UET.Commands.Upgrade;

if (Environment.GetEnvironmentVariable("CI") == "true")
{
    Crayon.Output.Enable();
}

// Construct the root command. We have to do this to see what command the user
// is invoking, to make sure we don't do the BuildConfig.json-based version switch
// if the user is invoking a "global" command.
var rootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
var globalCommands = new HashSet<Command>();
rootCommand.AddOption(UET.Commands.CommandExtensions.GetTraceOption());
rootCommand.AddCommand(BuildCommand.CreateBuildCommand());
rootCommand.AddCommand(TestCommand.CreateTestCommand());
rootCommand.AddCommand(GenerateCommand.CreateGenerateCommand());
rootCommand.AddCommand(ConfigCommand.CreateConfigCommand());
rootCommand.AddCommand(FormatCommand.CreateFormatCommand());
rootCommand.AddCommand(ListCommand.CreateListCommand());
rootCommand.AddCommand(InstallSdksCommand.CreateInstallSdksCommand());
rootCommand.AddCommand(UpgradeCommand.CreateUpgradeCommand(globalCommands));
rootCommand.AddCommand(StorageCommand.CreateStorageCommand(globalCommands));
rootCommand.AddCommand(UefsCommand.CreateUefsCommand());
rootCommand.AddCommand(TransferCommand.CreateTransferCommand());
rootCommand.AddCommand(AppleCertCommand.CreateAppleCertCommand());
rootCommand.AddCommand(CMakeCommand.CreateCMakeCommand());
rootCommand.AddCommand(InternalCommand.CreateInternalCommand(globalCommands));

// If we have an implicit command variable, this is an internal command where we can't specify arguments directly.
var implicitCommand = Environment.GetEnvironmentVariable("UET_IMPLICIT_COMMAND");
if (!string.IsNullOrWhiteSpace(implicitCommand))
{
    // Clear it for any downstream processes we might start.
    Environment.SetEnvironmentVariable("UET_IMPLICIT_COMMAND", null);

    // Prepend to args.
    args = new[] { "internal", implicitCommand }.Concat(args).ToArray();
}

// Parse the command line so we can inspect it.
var parseResult = rootCommand.Parse(args);
var isGlobalCommand = globalCommands.Contains(parseResult.CommandResult.Command);

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
        services.AddUETCore(permitRunbackLogging: args.Contains("ci-build", StringComparer.Ordinal));
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
                            var upgradeRootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
                            upgradeRootCommand.AddCommand(UpgradeCommand.CreateUpgradeCommand(new HashSet<Command>()));
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
                        await Console.Out.FlushAsync().ConfigureAwait(false);
                        await Console.Error.FlushAsync().ConfigureAwait(false);
                        Environment.Exit(nestedExitCode);
                        throw new BadImageFormatException();
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
        macosXcodeSelectServices.AddUETCore(permitRunbackLogging: args.Contains("ci-build", StringComparer.Ordinal));
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

// We didn't re-execute into a different version of UET. Invoke the originally requested command.
// @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
var exitCode = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
await Console.Out.FlushAsync().ConfigureAwait(false);
await Console.Error.FlushAsync().ConfigureAwait(false);
Environment.Exit(exitCode);
throw new BadImageFormatException();