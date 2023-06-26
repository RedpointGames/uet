using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.ProcessExecution;
using Redpoint.UET.Core;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UET.Commands.Build;
using UET.Commands.Internal;
using UET.Commands.List;
using UET.Commands.Test;
using UET.Commands.Uefs;
using UET.Commands.Upgrade;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

// Construct the root command. We have to do this to see what command the user
// is invoking, to make sure we don't do the BuildConfig.json-based version switch
// if the user is invoking a "global" command.
var rootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
var globalCommands = new HashSet<Command>();
rootCommand.AddOption(UET.Commands.CommandExtensions.GetTraceOption());
rootCommand.AddCommand(BuildCommand.CreateBuildCommand());
rootCommand.AddCommand(TestCommand.CreateTestCommand());
rootCommand.AddCommand(ListCommand.CreateListCommand());
rootCommand.AddCommand(UpgradeCommand.CreateUpgradeCommand(globalCommands));
rootCommand.AddCommand(UefsCommand.CreateUefsCommand());
rootCommand.AddCommand(InternalCommand.CreateInternalCommand(globalCommands));

// Parse the command line so we can inspect it.
var parseResult = rootCommand.Parse(args);
var isGlobalCommand = globalCommands.Contains(parseResult.CommandResult.Command);

// If we have a BuildConfig.json file in this folder, and that file specifies a
// UETVersion, then we must use that version specifically.
if (!isGlobalCommand && Environment.GetEnvironmentVariable("UET_RUNNING_UNDER_BUILDGRAPH") != "true" &&
    Environment.GetEnvironmentVariable("UET_VERSION_CHECK_COMPLETE") != "true")
{
    var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
    var currentVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    string? targetVersion = null;
    if (File.Exists(currentBuildConfigPath) && currentVersionAttribute != null)
    {
        try
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath));
            targetVersion = document!.AsObject()["UETVersion"]!.ToString();
        }
        catch
        {
        }

        var services = new ServiceCollection();
        services.AddUETCore();
        services.AddProcessExecution();
        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<Program>>();
        var processExecutor = sp.GetRequiredService<IProcessExecutor>();

        var versionRegex = new Regex("^[0-9\\.]+$");
        if (targetVersion != null && targetVersion != "BleedingEdge" && !versionRegex.IsMatch(targetVersion))
        {
            logger.LogError($"The BuildConfig.json file requested version '{targetVersion}', but this isn't a valid version string.");
            return 1;
        }

        if (targetVersion != null && (targetVersion != currentVersionAttribute.InformationalVersion || targetVersion == "BleedingEdge"))
        {
            if (Debugger.IsAttached)
            {
                logger.LogWarning($"The BuildConfig.json file requested version {targetVersion}, but we are running under a debugger, so this is being ignored.");
            }
            else if (currentVersionAttribute.InformationalVersion.EndsWith("-pre"))
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
                    logger.LogInformation($"The BuildConfig.json file requested version {targetVersion}, but we are running {currentVersionAttribute.InformationalVersion}. Obtaining the right version for this build and re-executing the requested command as version {targetVersion}...");
                }
                var didInstall = false;
                var isBleedingEdgeTheSame = false;
                try
                {
                    var upgradeRootCommand = new RootCommand("An unofficial tool for Unreal Engine.");
                    upgradeRootCommand.AddCommand(UpgradeCommand.CreateUpgradeCommand(new HashSet<Command>()));
                    var upgradeArgs = new[] { "upgrade", "--version", targetVersion!, "--do-not-set-as-current" };
                    if (targetVersion == "BleedingEdge")
                    {
                        upgradeArgs = new[] { "upgrade", "--do-not-set-as-current" };
                    }
                    var upgradeResult = await upgradeRootCommand.InvokeAsync(upgradeArgs);
                    if (upgradeResult != 0)
                    {
                        logger.LogError($"Failed to install the requested UET version {targetVersion}. See above for details.");
                        return 1;
                    }

                    didInstall = true;
                    if (targetVersion == "BleedingEdge")
                    {
                        targetVersion = UpgradeCommand.LastInstalledVersion!;
                        if (targetVersion == currentVersionAttribute.InformationalVersion)
                        {
                            isBleedingEdgeTheSame = true;
                        }
                        else
                        {
                            logger.LogInformation($"The bleeding-edge version of UET is {targetVersion}, but we are running {currentVersionAttribute.InformationalVersion}. Re-executing the requested command as version {targetVersion}...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to install the requested UET version {targetVersion}. Exception was: {ex.Message}");
                    return 1;
                }

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
                            FilePath = UpgradeCommand.GetAssemblyPathForVersion(targetVersion),
                            Arguments = args,
                            WorkingDirectory = Environment.CurrentDirectory,
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "UET_VERSION_CHECK_COMPLETE", "true" }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cts.Token);
                    await Console.Out.FlushAsync();
                    await Console.Error.FlushAsync();
                    Environment.Exit(nestedExitCode);
                    throw new BadImageFormatException();
                }
            }
        }
    }
}

// We didn't re-execute into a different version of UET. Invoke the originally requested command.
// @note: We use Environment.Exit so fire-and-forget tasks that contain stallable code won't prevent the process from exiting.
var exitCode = await rootCommand.InvokeAsync(args);
await Console.Out.FlushAsync();
await Console.Error.FlushAsync();
Environment.Exit(exitCode);
throw new BadImageFormatException();