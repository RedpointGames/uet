namespace Redpoint.Uet.Uat
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.OpenGE;
    using Redpoint.Uet.Uat.Internal;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class DefaultUATExecutor : IUATExecutor
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<DefaultUATExecutor> _logger;
        private readonly IBuildConfigurationManager _buildConfigurationManager;
        private readonly ILocalHandleCloser _localHandleCloser;
        private readonly IRemoteHandleCloser _remoteHandleCloser;
        private readonly IPathResolver _pathResolver;
        private readonly IWorldPermissionApplier _worldPermissionApplier;

        internal class ScriptModuleJson
        {
            [JsonPropertyName("ProjectPath")]
            public string? ProjectPath { get; set; }

            [JsonPropertyName("TargetPath")]
            public string? TargetPath { get; set; }
        }

        public DefaultUATExecutor(
            IProcessExecutor processExecutor,
            ILogger<DefaultUATExecutor> logger,
            IBuildConfigurationManager buildConfigurationManager,
            ILocalHandleCloser localHandleCloser,
            IRemoteHandleCloser remoteHandleCloser,
            IPathResolver pathResolver,
            IWorldPermissionApplier worldPermissionApplier)
        {
            _processExecutor = processExecutor;
            _logger = logger;
            _buildConfigurationManager = buildConfigurationManager;
            _localHandleCloser = localHandleCloser;
            _remoteHandleCloser = remoteHandleCloser;
            _pathResolver = pathResolver;
            _worldPermissionApplier = worldPermissionApplier;
        }

        public async Task<int> ExecuteAsync(
            string enginePath,
            UATSpecification uatSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            // Check to see if any script modules are missing DLLs; we automatically delete these since at
            // least one restricted platform does not correctly package it's script module when making an
            // installed build.
            var scriptModulesPath = Path.Combine(enginePath, "Engine", "Intermediate", "ScriptModules");
            if (Directory.Exists(scriptModulesPath))
            {
                foreach (var scriptModuleFullName in Directory.GetFiles(scriptModulesPath, "*.json"))
                {
                    var json = JsonSerializer.Deserialize<ScriptModuleJson>(
                        await File.ReadAllTextAsync(scriptModuleFullName, cancellationToken).ConfigureAwait(false),
                        ScriptModuleJsonSourceGenerationContext.Default.ScriptModuleJson);
                    if (json == null || string.IsNullOrWhiteSpace(json.ProjectPath) || string.IsNullOrWhiteSpace(json.TargetPath))
                    {
                        continue;
                    }
                    var projectPath = Path.Combine(scriptModulesPath, json.ProjectPath);
                    var targetPath = Path.Combine(Path.GetDirectoryName(projectPath)!, json.TargetPath);
                    if (!Path.Exists(targetPath))
                    {
                        _logger.LogInformation($"Automatically removing script module {scriptModuleFullName} as the target assembly does not exist!");
                        File.Delete(scriptModuleFullName);
                    }
                }
            }

            // Try to make the folder that BuildGraph will emit copy logs to.
            try
            {
                Directory.CreateDirectory(Path.Combine(enginePath, "Engine", "Programs", "AutomationTool", "Saved", "Logs"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Unable to create directory for BuildGraph automation logs! {ex}");
            }

            // We have to mangle some arguments due to bugs in UAT.
            var finalArgs = new List<string>();
            var doScriptWorkaround = false;
            foreach (var arg in uatSpecification.Arguments)
            {
                if (uatSpecification.Command == "BuildGraph" && arg.StartsWith("-Script=", StringComparison.Ordinal) && arg.EndsWith("Engine\\Build\\InstalledEngineBuild.xml", StringComparison.Ordinal))
                {
                    // Hack for UE5 engine builds that don't include console platforms properly
                    // if the script is an absolute path.
                    finalArgs.Add("-Script=Engine/Build/InstalledEngineBuild.xml");
                    doScriptWorkaround = true;
                }
                else
                {
                    finalArgs.Add(arg);
                }
            }

            // Check if we need to detect for lost file handles on Windows for shared drives.
            var singleNodeName = string.Empty;
            var sharedStorageDir = string.Empty;
            var requireLostHandleDetection = false;
            var cleanupTargetDirectoryBeforeExecution = false;
            if (uatSpecification.Command == "BuildGraph")
            {
                foreach (var arg in finalArgs)
                {
                    if (arg.StartsWith("-SingleNode=", StringComparison.Ordinal))
                    {
                        singleNodeName = arg["-SingleNode=".Length..].Replace("\"", "", StringComparison.Ordinal);
                    }
                    else if (arg.StartsWith("-SharedStorageDir=", StringComparison.Ordinal))
                    {
                        sharedStorageDir = arg["-SharedStorageDir=".Length..].Replace("\"", "", StringComparison.Ordinal);
                    }
                }
                if (!string.IsNullOrWhiteSpace(singleNodeName) && !string.IsNullOrWhiteSpace(sharedStorageDir))
                {
                    cleanupTargetDirectoryBeforeExecution = true;
                }
                if (OperatingSystem.IsWindows())
                {
                    if (!string.IsNullOrWhiteSpace(singleNodeName) && !string.IsNullOrWhiteSpace(sharedStorageDir) &&
                        !sharedStorageDir.StartsWith($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", StringComparison.Ordinal))
                    {
                        requireLostHandleDetection = true;
                    }
                }
            }

            // Detect if we have BUILD_GRAPH_PROJECT_ROOT set for this process.
            string? buildGraphProjectRoot = null;
            if (uatSpecification.EnvironmentVariables != null)
            {
                if (uatSpecification.EnvironmentVariables.ContainsKey("BUILD_GRAPH_PROJECT_ROOT"))
                {
                    buildGraphProjectRoot = uatSpecification.EnvironmentVariables["BUILD_GRAPH_PROJECT_ROOT"];
                    if (string.IsNullOrWhiteSpace(buildGraphProjectRoot))
                    {
                        buildGraphProjectRoot = null;
                    }
                }
            }

            // Execute UAT, automatically handling retries as needed.
            var didMutateBuildConfiguration = await _buildConfigurationManager.PushBuildConfiguration().ConfigureAwait(false);
            try
            {
                int reportedExitCode = -1;
                while (true)
                {
                    // Clean up the target directory before we run UAT if this is a run of BuildGraph.
                    if (cleanupTargetDirectoryBeforeExecution)
                    {
                        var targetPath = Path.Combine(sharedStorageDir, singleNodeName);
                        do
                        {
                            try
                            {
                                if (buildGraphProjectRoot != null)
                                {
                                    var localOutputPath = Path.Combine(buildGraphProjectRoot, "Engine", "Saved", "BuildGraph", singleNodeName);
                                    if (Directory.Exists(localOutputPath) &&
                                        Directory.GetFileSystemEntries(localOutputPath).Any())
                                    {
                                        _logger.LogWarning($"Detected existing local output directory at '{localOutputPath}'. Deleting contents...");
                                        try
                                        {
                                            await DirectoryAsync.DeleteAsync(localOutputPath, true).ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            _logger.LogWarning($"Failed to delete '{localOutputPath}'. Forcibly closing handles...");
                                            await _localHandleCloser.CloseLocalHandles(localOutputPath).ConfigureAwait(false);

                                            _logger.LogWarning($"Handles closed for '{localOutputPath}'. Trying to delete again...");
                                            await DirectoryAsync.DeleteAsync(localOutputPath, true).ConfigureAwait(false);
                                        }
                                    }
                                }

                                if (Directory.Exists(targetPath) &&
                                    Directory.GetFileSystemEntries(targetPath).Any())
                                {
                                    _logger.LogWarning($"Detected existing output directory at '{targetPath}'. Deleting contents...");
                                    try
                                    {
                                        await DirectoryAsync.DeleteAsync(targetPath, true).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        _logger.LogWarning($"Failed to delete '{targetPath}'. Forcibly closing handles...");
                                        await _remoteHandleCloser.CloseRemoteHandles(targetPath).ConfigureAwait(false);
                                        await _localHandleCloser.CloseLocalHandles(targetPath).ConfigureAwait(false);

                                        _logger.LogWarning($"Handles closed for '{targetPath}'. Trying to delete again...");
                                        await DirectoryAsync.DeleteAsync(targetPath, true).ConfigureAwait(false);
                                    }
                                    Directory.CreateDirectory(targetPath);
                                    await _worldPermissionApplier.GrantEveryonePermissionAsync(targetPath, CancellationToken.None).ConfigureAwait(false);
                                }
                                break;
                            }
                            catch (Exception ex)
                            {
                                if (ex.Message.Contains("used by another process", StringComparison.Ordinal) || ex.Message.Contains("is denied", StringComparison.Ordinal))
                                {
                                    _logger.LogWarning("File lock still present for one or more files...");
                                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                                    continue;
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        }
                        while (true);
                    }

                    // Determine the process specification to use based on whether we're running on macOS/Linux or Windows.
                    OpenGEProcessSpecification processSpecification;
                    if (OperatingSystem.IsWindows())
                    {
                        processSpecification = new OpenGEProcessSpecification
                        {
                            FilePath = Path.Combine(enginePath, "Engine", "Build", "BatchFiles", "RunUAT.bat"),
                            Arguments = new[]
                            {
                                uatSpecification.Command,
                            }.Concat(finalArgs),
                            WorkingDirectory = doScriptWorkaround ? enginePath : uatSpecification.WorkingDirectory,
                            EnvironmentVariables = uatSpecification.EnvironmentVariables,
                            StdinData = uatSpecification.StdinData,
                            DisableOpenGE = uatSpecification.DisableOpenGE,
                        };
                    }
                    else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                    {
                        processSpecification = new OpenGEProcessSpecification
                        {
                            FilePath = Path.Combine(enginePath, "Engine", "Build", "BatchFiles", "RunUAT.sh"),
                            Arguments = new[]
                            {
                                uatSpecification.Command,
                            }.Concat(finalArgs),
                            WorkingDirectory = doScriptWorkaround ? enginePath : uatSpecification.WorkingDirectory,
                            EnvironmentVariables = uatSpecification.EnvironmentVariables,
                            StdinData = uatSpecification.StdinData,
                            DisableOpenGE = uatSpecification.DisableOpenGE,
                        };
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }

                    // Create the capture specification to track whether we need to 
                    var retryCaptureSpecification = new RetryCaptureSpecification(captureSpecification);

                    // Execute UAT.
                    reportedExitCode = await _processExecutor.ExecuteAsync(
                        processSpecification,
                        retryCaptureSpecification,
                        cancellationToken).ConfigureAwait(false);

                    // We need to check if BuildGraph didn't release file handles properly. When this happens, Windows holds open
                    // the file handle on the remote machine that the share is mapped to, and this causes failures on downstream
                    // builds that need the files as inputs. In this case, the files whose handles are lost are *also* not written
                    // correctly - they end up as 0 bytes if we forcibly close the handle. Therefore, we need to release the handles,
                    // delete the output directory on the shared drive, and then run this build again.
                    if (requireLostHandleDetection)
                    {
                        if (await _remoteHandleCloser.CloseRemoteHandles(Path.Combine(sharedStorageDir, singleNodeName)).ConfigureAwait(false))
                        {
                            _logger.LogWarning("Detected lost file handles for output. Automatically retrying...");
                            continue;
                        }
                    }

                    // If the reported exit code is non-zero and the output detected we need to retry, or if the output wants to force a retry, then do this build node again.
                    if ((reportedExitCode != 0 && retryCaptureSpecification.NeedsRetry) ||
                        (retryCaptureSpecification.ForceRetry))
                    {
                        _logger.LogWarning("Detected this build node needs to be retried.");
                        continue;
                    }

                    // If we didn't trigger the retry logic, break out of the while (true) loop.
                    break;
                }
                return reportedExitCode;
            }
            finally
            {
                if (didMutateBuildConfiguration)
                {
                    await _buildConfigurationManager.PopBuildConfiguration().ConfigureAwait(false);
                }
            }
        }
    }
}