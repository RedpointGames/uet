namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Export;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Gradle;
    using Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Core.BugReport;
    using Redpoint.Uet.Uat;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphExecutor : IBuildGraphExecutor
    {
        private readonly ILogger<DefaultBuildGraphExecutor> _logger;
        private readonly IUATExecutor _uatExecutor;
        private readonly IBuildGraphArgumentGenerator _buildGraphArgumentGenerator;
        private readonly IBuildGraphPatcher _buildGraphPatcher;
        private readonly IWorkspaceProvider _dynamicWorkspaceProvider;
        private readonly IMobileProvisioning _mobileProvisioning;
        private readonly IGradleWorkspace _gradleWorkspace;
        private readonly BugReportCollector? _bugReportCollector;
        private static int _invocationCount = 0;

        public DefaultBuildGraphExecutor(
            ILogger<DefaultBuildGraphExecutor> logger,
            IUATExecutor uatExecutor,
            IBuildGraphArgumentGenerator buildGraphArgumentGenerator,
            IBuildGraphPatcher buildGraphPatcher,
            IWorkspaceProvider dynamicWorkspaceProvider,
            IMobileProvisioning mobileProvisioning,
            IGradleWorkspace gradleWorkspace,
            BugReportCollector? bugReportCollector = null)
        {
            _logger = logger;
            _uatExecutor = uatExecutor;
            _buildGraphArgumentGenerator = buildGraphArgumentGenerator;
            _buildGraphPatcher = buildGraphPatcher;
            _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
            _mobileProvisioning = mobileProvisioning;
            _gradleWorkspace = gradleWorkspace;
            _bugReportCollector = bugReportCollector;
        }

        public async Task ListGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var exitCode = await InternalRunAsync(
                enginePath,
                string.Empty,
                string.Empty,
                string.Empty,
                buildGraphScript,
                string.Empty,
                string.Empty,
                new[] { $"-ListOnly" },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    { "IsBuildMachine", "1" },
                    { "uebp_LOCAL_ROOT", enginePath.TrimEnd('\\') },
                },
                null,
                captureSpecification,
                Array.Empty<string>(),
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new BuildGraphExecutionFailure($"Failed to list options from build graph; UAT exited with non-zero exit code {exitCode}.");
            }
        }

        public async Task<int> ExecuteGraphNodeAsync(
            string engineWorkspacePath,
            string targetWorkspacePath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphNodeName,
            string buildGraphSharedStorageDir,
            string? buildGraphTelemetryDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            Dictionary<string, string> globalEnvironmentVariables,
            IReadOnlyList<BuildConfigMobileProvision> mobileProvisions,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            await using ((await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
            {
                Name = "NuGetPackages"
            }, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var nugetPackages).ConfigureAwait(false))
            {
                GradleWorkspaceInstance? gradleInstance = null;
                if (buildGraphNodeName.Contains("Android", StringComparison.InvariantCultureIgnoreCase) ||
                    buildGraphNodeName.Contains("MetaQuest", StringComparison.InvariantCultureIgnoreCase) ||
                    buildGraphNodeName.Contains("GooglePlay", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Only set up and tear down the Gradle workspace if the node is for Android,
                    // since it's quite expensive to copy the cache back and forth (but necessary
                    // to mitigate Gradle concurrency bugs).
                    gradleInstance = await _gradleWorkspace.GetGradleWorkspaceInstance(cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    var environmentVariables = new Dictionary<string, string>
                    {
                        { "IsBuildMachine", "1" },
                        { "uebp_LOCAL_ROOT", engineWorkspacePath.TrimEnd('\\') },
                        // BuildGraph in Unreal Engine 5.0 causes input files to be unnecessarily modified. Just allow mutation since I'm not sure what the bug is.
                        { "BUILD_GRAPH_ALLOW_MUTATION", "true" },
                        // Make sure UET knows it's running under BuildGraph for subcommands
                        // so that we can emit the extra newline necessary for BuildGraph to
                        // show all output. Refer to CommandExtensions.cs to see where this
                        // is used.
                        { "UET_RUNNING_UNDER_BUILDGRAPH", "true" },
                        { "UET_XGE_SHIM_BUILD_NODE_NAME", buildGraphNodeName },
                        // Isolate NuGet package restore so that multiple jobs can restore at
                        // the same time.
                        { "NUGET_PACKAGES", nugetPackages.Path },
                    };
                    if (!string.IsNullOrWhiteSpace(buildGraphTelemetryDir))
                    {
                        environmentVariables["UE_TELEMETRY_DIR"] = buildGraphTelemetryDir;
                    }
                    if (gradleInstance != null)
                    {
                        // Adjust Gradle cache path so that Android packaging works under SYSTEM.
                        environmentVariables["GRADLE_USER_HOME"] = gradleInstance.GradleHomePath;
                    }

                    // If the engine is a source engine, place the target into the engine via a symbolic link, and use the engine
                    // as the BuildGraph project root. This ensures that editor binaries are included in uploads to shared storage, such
                    // that builds on other nodes can download the required editor binaries. If we didn't do this, then cook steps will
                    // fail as only the editor modules for the game will be staged, but not UnrealEditor.exe.
                    if (System.Environment.GetEnvironmentVariable("UET_USE_SYMBOLIC_TARGET_WORKSPACE_FOR_SOURCE_ENGINE") == "1" &&
                        File.Exists(Path.Combine(engineWorkspacePath, "Engine", "Build", "SourceDistribution.txt")) &&
                        !string.IsNullOrWhiteSpace(targetWorkspacePath))
                    {
                        var symbolicLink = Path.Combine(engineWorkspacePath, "P");
                        _logger.LogInformation($"Setting up a symbolic link from '{symbolicLink}' to '{targetWorkspacePath}' to build project in the engine workspace...");
                        var existingDirectory = new DirectoryInfo(symbolicLink);
                        if (existingDirectory.Exists)
                        {
                            if (existingDirectory.LinkTarget != null)
                            {
                                _logger.LogInformation($"Deleting existing symbolic link at '{symbolicLink}'...");
                                existingDirectory.Delete();
                            }
                            else
                            {
                                _logger.LogError($"Directory exists at '{symbolicLink}', but it's not a symbolic link so we're not deleting it to be safe. Please fix up the engine workspace and remove this directory for builds to succeed.");
                                return 1;
                            }
                        }
                        _logger.LogInformation($"Creating new symbolic link at '{symbolicLink}'...");
                        Directory.CreateSymbolicLink(
                            symbolicLink,
                            targetWorkspacePath);

                        var projectDirsFile = Path.Combine(engineWorkspacePath, "UET.uprojectdirs");
                        _logger.LogInformation($"Creating project directory file at '{projectDirsFile}'...");
                        await File.WriteAllTextAsync(
                            projectDirsFile,
                            """
                            ; Search in symbolic link directory set up by UET.
                            P
                            """,
                            cancellationToken);

                        _logger.LogInformation($"Changing target workspace path to '{symbolicLink}' for consistency...");
                        environmentVariables["BUILD_GRAPH_PROJECT_ROOT"] = engineWorkspacePath.TrimEnd('\\');
                        targetWorkspacePath = symbolicLink;
                    }
                    // Otherwise, set the BUILD_GRAPH_PROJECT_ROOT based on whether we're building an engine or not.
                    else if (!string.IsNullOrWhiteSpace(targetWorkspacePath))
                    {
                        environmentVariables["BUILD_GRAPH_PROJECT_ROOT"] = targetWorkspacePath;
                    }
                    else
                    {
                        environmentVariables["BUILD_GRAPH_PROJECT_ROOT"] = engineWorkspacePath.TrimEnd('\\');
                    }
                    if (string.IsNullOrWhiteSpace(environmentVariables["BUILD_GRAPH_PROJECT_ROOT"]))
                    {
                        throw new InvalidOperationException("BUILD_GRAPH_PROJECT_ROOT is empty, when it should be set to either the repository root or engine path.");
                    }
                    else
                    {
                        _logger.LogInformation($"BuildGraph is executing with BUILD_GRAPH_PROJECT_ROOT={environmentVariables["BUILD_GRAPH_PROJECT_ROOT"]}");
                    }
                    foreach (var kv in globalEnvironmentVariables)
                    {
                        environmentVariables[kv.Key] = kv.Value;
                    }

                    // BuildGraph prefers to re-use local manifests if they exist at BUILD_GRAPH_PROJECT_ROOT/Engine/Saved/BuildGraph instead of going to shared storage. If it finds them, it does not download from shared storage and instead assumes that we're executing in a working tree that hasn't been modified since that previous node ran. However, this causes issues when multiple nodes write to the same file and then execute on the same machine:
                    //
                    // - Node A runs, writes and tags a file, which gets sent to shared storage.
                    // - Node B runs, writes and tags the same file but with different content, which gets sent to shared storage.
                    // - Node C which depends on Node A sees the local manifest of Node A, skips downloading from shared storage, and then errors out because the file it wants from Node A is actually Node B's version and has a different length/content.
                    //
                    // To prevent this from happening, we delete the BUILD_GRAPH_PROJECT_ROOT/Engine/Saved/BuildGraph directory if it exists, before and after every execution of BuildGraph, which prevents stale local files from being used.
                    //
                    // @note: All executions of BuildGraph - even local builds - use a shared storage folder under UET, so we don't need these local manifest files anyway.

                    var buildGraphLocalManifestPath = Path.Combine(environmentVariables["BUILD_GRAPH_PROJECT_ROOT"], "Engine", "Saved", "BuildGraph");
                    if (Directory.Exists(buildGraphLocalManifestPath))
                    {
                        await DirectoryAsync.DeleteAsync(buildGraphLocalManifestPath, true).ConfigureAwait(false);
                    }
                    try
                    {
                        var exitCode = await InternalRunAsync(
                            engineWorkspacePath,
                            targetWorkspacePath,
                            uetPath,
                            artifactExportPath,
                            buildGraphScript,
                            buildGraphTarget,
                            buildGraphSharedStorageDir,
                            new[]
                            {
                                $"-SingleNode={buildGraphNodeName}",
                                "-WriteToSharedStorage",
                                $"-SharedStorageDir={buildGraphSharedStorageDir}"
                            },
                            buildGraphArguments,
                            buildGraphArgumentReplacements,
                            environmentVariables,
                            mobileProvisions,
                            captureSpecification,
                            buildGraphNodeName.Contains("Pak and Stage", StringComparison.OrdinalIgnoreCase)
                                ? ["had to patch your engine"]
                                : Array.Empty<string>(),
                            cancellationToken).ConfigureAwait(false);
                        if (exitCode == 0)
                        {
                            gradleInstance?.MarkBuildAsSuccessful();
                        }
                        return exitCode;
                    }
                    finally
                    {
                        if (Directory.Exists(buildGraphLocalManifestPath))
                        {
                            await DirectoryAsync.DeleteAsync(buildGraphLocalManifestPath, true).ConfigureAwait(false);
                        }
                    }
                }
                catch
                {
                    if (gradleInstance != null)
                    {
                        await gradleInstance.DisposeAsync().ConfigureAwait(false);
                    }
                    throw;
                }
            }
        }

        public async Task<BuildGraphExport> GenerateGraphAsync(
            string engineWorkspacePath,
            string targetWorkspacePath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphSharedStorageDir,
            string? buildGraphTelemetryDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var buildGraphOutput = Path.GetTempFileName();
            var deleteBuildGraphOutput = true;
            try
            {
                var environmentVariables = new Dictionary<string, string>
                {
                    { "IsBuildMachine", "1" },
                    { "uebp_LOCAL_ROOT", engineWorkspacePath.TrimEnd('\\') },
                };
                if (!string.IsNullOrWhiteSpace(buildGraphTelemetryDir))
                {
                    environmentVariables["UE_TELEMETRY_DIR"] = buildGraphTelemetryDir;
                }

                var exitCode = await InternalRunAsync(
                    engineWorkspacePath,
                    targetWorkspacePath,
                    uetPath,
                    artifactExportPath,
                    buildGraphScript,
                    buildGraphTarget,
                    buildGraphSharedStorageDir,
                    new[] { $"-Export={buildGraphOutput}" },
                    buildGraphArguments,
                    buildGraphArgumentReplacements,
                    environmentVariables,
                    null,
                    captureSpecification,
                    Array.Empty<string>(),
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT exited with non-zero exit code {exitCode}.");
                }

                using (var reader = new FileStream(buildGraphOutput, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try
                    {
                        var json = JsonSerializer.Deserialize<BuildGraphExport>(reader, BuildGraphSourceGenerationContext.Default.BuildGraphExport);
                        if (json == null)
                        {
                            deleteBuildGraphOutput = false;
                            throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT did not produce a valid BuildGraph JSON file. Output file is stored at: '{buildGraphOutput}'.");
                        }
                        return json;
                    }
                    catch (JsonException ex)
                    {
                        deleteBuildGraphOutput = false;
                        throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT did not produce a valid BuildGraph JSON file. Output file is stored at: '{buildGraphOutput}'. Original exception was: {ex}");
                    }
                }
            }
            finally
            {
                if (deleteBuildGraphOutput)
                {
                    File.Delete(buildGraphOutput);
                }
            }
        }

        private async Task<int> InternalRunAsync(
            string engineWorkspacePath,
            string targetWorkspacePath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphSharedStorageDir,
            string[] internalArguments,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            Dictionary<string, string> buildGraphEnvironmentVariables,
            IReadOnlyList<BuildConfigMobileProvision>? mobileProvisions,
            ICaptureSpecification captureSpecification,
            string[] forceRetryMessages,
            CancellationToken cancellationToken)
        {
            string buildGraphScriptPath;
            var deleteBuildGraphScriptPath = false;
            if (buildGraphScript._forEngine)
            {
                buildGraphScriptPath = Path.Combine(
                    engineWorkspacePath,
                    "Engine",
                    "Build",
                    "InstalledEngineBuild.xml");
            }
            else if (buildGraphScript._forPlugin)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.BuildGraph_Plugin.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else if (buildGraphScript._forProject)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.BuildGraph_Project.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else
            {
                throw new NotSupportedException();
            }

            await _buildGraphPatcher.PatchBuildGraphAsync(engineWorkspacePath, buildGraphScript._forEngine).ConfigureAwait(false);

            if (mobileProvisions != null)
            {
                await _mobileProvisioning.InstallMobileProvisions(engineWorkspacePath, buildGraphScript._forEngine, mobileProvisions, cancellationToken).ConfigureAwait(false);
            }

            if (buildGraphEnvironmentVariables.Count == 0)
            {
                _logger.LogTrace("Executing BuildGraph with no environment variables.");
            }
            else
            {
                _logger.LogTrace($"Executing BuildGraph with the following environment variables:");
                foreach (var kv in buildGraphEnvironmentVariables)
                {
                    _logger.LogTrace($"  {kv.Key}={kv.Value}");
                }
            }

            var localInvocationCount = ++_invocationCount;
            if (!string.IsNullOrWhiteSpace(buildGraphScriptPath))
            {
                _bugReportCollector?.CollectFileForBugReport(
                    buildGraphScriptPath,
                    $"BuildGraph-Invocation-{localInvocationCount}.xml");
            }

            try
            {
                return await _uatExecutor.ExecuteAsync(
                    engineWorkspacePath,
                    new UATSpecification
                    {
                        Command = "BuildGraph",
                        Arguments = Array.Empty<LogicalProcessArgument>()
                            .Concat(string.IsNullOrWhiteSpace(buildGraphTarget) ? Array.Empty<LogicalProcessArgument>() : [$"-Target={buildGraphTarget}"])
                            .Concat(
                            [
                                "-noP4",
                            ])
                            .Concat(string.IsNullOrWhiteSpace(buildGraphScriptPath) ? Array.Empty<LogicalProcessArgument>() : [$"-Script={buildGraphScriptPath}"])
                            .Concat(internalArguments.Select(x => new LogicalProcessArgument(x)))
                            .Concat(_buildGraphArgumentGenerator.GenerateBuildGraphArguments(
                                buildGraphArguments,
                                buildGraphArgumentReplacements,
                                targetWorkspacePath,
                                uetPath,
                                engineWorkspacePath,
                                buildGraphSharedStorageDir,
                                artifactExportPath).Select(x => new LogicalProcessArgument(x))),
                        EnvironmentVariables = buildGraphEnvironmentVariables
                    },
                    captureSpecification,
                    forceRetryMessages,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (deleteBuildGraphScriptPath)
                {
                    File.Delete(buildGraphScriptPath);
                }

                var logPath = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "Unreal Engine",
                    "AutomationTool",
                    "Logs",
                    engineWorkspacePath
                        .TrimEnd(Path.DirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '+')
                        .Replace(' ', '+')
                        .Replace(":", "", StringComparison.Ordinal),
                    "Log.txt");
                if (File.Exists(logPath))
                {
                    _bugReportCollector?.CollectFileForBugReport(
                        logPath,
                        $"BuildGraph-Invocation-{localInvocationCount}.log");
                }
                else if (_bugReportCollector != null)
                {
                    _logger.LogWarning($"Unable to locate AutomationTool log for bug report collection: {logPath}");
                }
            }
        }
    }
}
