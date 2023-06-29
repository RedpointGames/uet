using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.ProcessExecution;
using Redpoint.ProgressMonitor;
using Redpoint.Logging.SingleLine;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UET.Commands.Upgrade;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddSingleLineConsoleFormatter(options =>
    {
        options.OmitLogPrefix = false;
    });
    builder.AddSingleLineConsole();
});
services.AddProcessExecution();
services.AddProgressMonitor();
var sp = services.BuildServiceProvider();
var logger = sp.GetRequiredService<ILogger<Program>>();
var processExecutor = sp.GetRequiredService<IProcessExecutor>();

var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
var currentVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
string? targetVersion = null;

// Try to compute the target version we want.
if (File.Exists(currentBuildConfigPath))
{
    // We have a BuildConfig.json in the current directory; use whatever is in there.
    try
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath));
        targetVersion = document!.AsObject()["UETVersion"]!.ToString();
    }
    catch
    {
    }
    if (!string.IsNullOrEmpty(targetVersion))
    {
        logger.LogInformation($"UET shim selected version {targetVersion} from BuildConfig.json.");
    }
}

// If we have a current version of UET installed, use that.
if (string.IsNullOrEmpty(targetVersion) && File.Exists(UpgradeCommandImplementation.GetAssemblyPathForVersion("Current")))
{
    targetVersion = FileVersionInfo.GetVersionInfo(UpgradeCommandImplementation.GetAssemblyPathForVersion("Current")).ProductVersion;
    if (!string.IsNullOrEmpty(targetVersion))
    {
        logger.LogInformation($"UET shim selected version {targetVersion} from current installations.");
    }
}

// If we have our own version attribute, use that.
if (string.IsNullOrEmpty(targetVersion) && currentVersionAttribute != null)
{
    targetVersion = currentVersionAttribute.InformationalVersion;
    if (targetVersion != null && targetVersion.EndsWith("-pre"))
    {
        // Can't select a pre-release version number.
        targetVersion = null;
    }
    if (!string.IsNullOrEmpty(targetVersion))
    {
        logger.LogInformation($"UET shim selected version {targetVersion} from shim's own version.");
    }
}

// Otherwise, pick the BleedingEdge.
if (string.IsNullOrEmpty(targetVersion))
{
    targetVersion = "BleedingEdge";
    logger.LogInformation($"UET shim selected version {targetVersion} as a last resort.");
}

// Make sure this is a valid version number.
var versionRegex = new Regex("^[0-9\\.]+$");
if (targetVersion == null || (targetVersion != "BleedingEdge" && !versionRegex.IsMatch(targetVersion)))
{
    logger.LogError($"The version selected by the UET shim ('{targetVersion}') was not a valid version string. This is a bug in the UET shim.");
    return 1;
}

// If this is BleedingEdge, or we don't have the target version installed, install it now.
if (targetVersion == "BleedingEdge" || !File.Exists(UpgradeCommandImplementation.GetAssemblyPathForVersion(targetVersion)))
{
    try
    {
        await UpgradeCommandImplementation.PerformUpgradeAsync(
            sp.GetRequiredService<IProgressFactory>(),
            sp.GetRequiredService<IMonitorFactory>(),
            logger,
            targetVersion,
            true);
        if (targetVersion == "BleedingEdge")
        {
            targetVersion = UpgradeCommandImplementation.LastInstalledVersion!;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to install the shim determined UET version {targetVersion}. Exception was: {ex.Message}");
        return 1;
    }
}

// We should now have the target version installed. If we don't, something went wrong.
if (!File.Exists(UpgradeCommandImplementation.GetAssemblyPathForVersion(targetVersion)))
{
    logger.LogError($"Expected the file '{UpgradeCommandImplementation.GetAssemblyPathForVersion(targetVersion)}' to exist after the shim installed UET, but it did not. This is a bug in the UET shim.");
    return 1;
}

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