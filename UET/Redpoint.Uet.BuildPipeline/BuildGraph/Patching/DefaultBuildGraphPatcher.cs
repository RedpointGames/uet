namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Hashing;
    using Redpoint.IO;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Uat;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphPatcher : IBuildGraphPatcher
    {
        private readonly ILogger<DefaultBuildGraphPatcher> _logger;
        private readonly IMSBuildPathResolver _msBuildPathResolver;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IDynamicWorkspaceProvider _dynamicWorkspaceProvider;
        private readonly IUATExecutor _uatExecutor;
        private readonly BuildGraphPatchSet[] _patches;
        private readonly string _patchHash;

        // Increment this whenever we have to reapply patches due to the logic in this class changing.
        private const int _patchCodeVersion = 2;

        public DefaultBuildGraphPatcher(
            ILogger<DefaultBuildGraphPatcher> logger,
            IMSBuildPathResolver msBuildPathResolver,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IDynamicWorkspaceProvider dynamicWorkspaceProvider,
            IUATExecutor uatExecutor)
        {
            _logger = logger;
            _msBuildPathResolver = msBuildPathResolver;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
            _uatExecutor = uatExecutor;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.Patching.BuildGraphPatches.json"))
            {
                _patches = JsonSerializer.Deserialize<BuildGraphPatchSet[]>(stream!, BuildGraphSourceGenerationContext.Default.BuildGraphPatchSetArray)!;
                stream!.Seek(0, SeekOrigin.Begin);
                _patchHash = Hash.Sha1AsHexString(stream);
            }
        }

        private static async Task MakeReadWriteAsync(DirectoryInfo di)
        {
            foreach (var subdirectory in di.GetDirectories())
            {
                if (subdirectory.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    subdirectory.Attributes = subdirectory.Attributes ^ FileAttributes.ReadOnly;
                }
                await MakeReadWriteAsync(subdirectory).ConfigureAwait(false);
            }
            foreach (var file in di.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    file.Attributes = file.Attributes ^ FileAttributes.ReadOnly;
                }
            }
        }

        private async Task CopyMissingEngineBitsAsync(string enginePath)
        {
            // Copy binaries that are missing from installed engine builds which are
            // necessary to rebuild UBT bits.
            var manifestResourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            var copyRules = new List<(string source, string target)>
            {
                (
                    source: $"stream:{manifestResourceNames.First(x => x.EndsWith(".fastJSON.dll", StringComparison.InvariantCultureIgnoreCase))}",
                    target: $"{enginePath}/Engine/Binaries/ThirdParty/fastJSON/netstandard2.0/fastJSON.dll"
                ),
                (
                    source: $"stream:{manifestResourceNames.First(x => x.EndsWith(".fastJSON.deps.json", StringComparison.InvariantCultureIgnoreCase))}",
                    target: $"{enginePath}/Engine/Binaries/ThirdParty/fastJSON/netstandard2.0/fastJSON.deps.json"
                ),
                (
                    source: $"stream:{manifestResourceNames.First(x => x.EndsWith(".Ionic.Zip.Reduced.dll", StringComparison.InvariantCultureIgnoreCase))}",
                    target: $"{enginePath}/Engine/Binaries/DotNET/Ionic.Zip.Reduced.dll"
                ),
                (
                    source: $"stream:{manifestResourceNames.First(x => x.EndsWith(".OneSky.dll", StringComparison.InvariantCultureIgnoreCase))}",
                    target: $"{enginePath}/Engine/Binaries/DotNET/OneSky.dll"
                ),
            };
            if (Directory.Exists($"{enginePath}/Engine/Source/Programs/Shared/EpicGames.Perforce.Native"))
            {
                // Copy EpicGames.Perforce.Native from the engine itself.
                copyRules.AddRange(new[]
                {
                    (
                        source: $"{enginePath}/Engine/Binaries/DotNET/AutomationTool/AutomationUtils/EpicGames.Perforce.Native.dll",
                        target: $"{enginePath}/Engine/Binaries/DotNET/EpicGames.Perforce.Native/win-x64/Release/EpicGames.Perforce.Native.dll"
                    ),
                    (
                        source: $"{enginePath}/Engine/Binaries/DotNET/AutomationTool/AutomationUtils/EpicGames.Perforce.Native.dylib",
                        target: $"{enginePath}/Engine/Binaries/DotNET/EpicGames.Perforce.Native/mac-x64/Release/EpicGames.Perforce.Native.dylib"
                    ),
                    (
                        source: $"{enginePath}/Engine/Binaries/DotNET/AutomationTool/AutomationUtils/EpicGames.Perforce.Native.so",
                        target: $"{enginePath}/Engine/Binaries/DotNET/EpicGames.Perforce.Native/linux-x64/Release/EpicGames.Perforce.Native.so"
                    ),
                });
            }

            // Perform the copy operations.
            foreach (var copyRule in copyRules)
            {
                var source = copyRule.source;
                var target = copyRule.target;
                if (OperatingSystem.IsWindows())
                {
                    source = source.Replace('/', '\\');
                    target = target.Replace('/', '\\');
                }
                if (!File.Exists(target))
                {
                    Stream sourceStream;
                    if (source.StartsWith("stream:", StringComparison.Ordinal))
                    {
                        sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(source["stream:".Length..])!;
                    }
                    else
                    {
                        if (!File.Exists(source))
                        {
                            continue;
                        }
                        sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    _logger.LogInformation($"Auto-fix: Need to copy '{target}' into place...");
                    using (sourceStream)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        using (var targetStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task PatchBuildGraphAsync(string enginePath, bool isEngineBuild)
        {
            var patchLevelFilePath = Path.Combine(enginePath, "Engine", "Source", "Programs", "UET.BuildGraphPatchLevel.json");
            var buildGraphPatchStatus = new BuildGraphPatchStatus();
            var applyPatches = false;
            if (File.Exists(patchLevelFilePath))
            {
                try
                {
                    buildGraphPatchStatus = JsonSerializer.Deserialize(
                        File.ReadAllText(patchLevelFilePath).Trim(),
                        BuildGraphPatchStatusJsonSerializerContext.Default.BuildGraphPatchStatus) ?? new BuildGraphPatchStatus();
                }
                catch
                {
                }
            }
            if (buildGraphPatchStatus.PatchHash != _patchHash)
            {
                _logger.LogInformation($"BuildGraph patch version is {buildGraphPatchStatus.PatchHash}, but the target patch version is {_patchHash}, applying patches...");
                applyPatches = true;
            }
            if (!applyPatches && buildGraphPatchStatus.PatchCodeVersion != _patchCodeVersion)
            {
                _logger.LogInformation($"BuildGraph patching code version is {buildGraphPatchStatus.PatchCodeVersion}, but the target patching code version is {_patchCodeVersion}, applying patches...");
                applyPatches = true;
            }
            var dateCheckPath = Path.Combine(enginePath, "Engine", "Binaries", "DotNET", "AutomationTool", "BuildGraph.Automation.dll");
            if (!applyPatches)
            {
                var dateCheckLastModified = File.Exists(dateCheckPath) ? new DateTimeOffset(File.GetLastWriteTimeUtc(dateCheckPath), TimeSpan.Zero).ToUnixTimeSeconds() : 0;
                if (buildGraphPatchStatus.BuildGraphAutomationDllLastModified != dateCheckLastModified)
                {
                    _logger.LogInformation($"BuildGraph expected last write timestamp is is {buildGraphPatchStatus.PatchCodeVersion}, but the actual last write timestamp is {dateCheckLastModified}, applying patches...");
                    applyPatches = true;
                }
            }

            if (!applyPatches)
            {
                _logger.LogInformation($"BuildGraph patches are already up-to-date at version {_patchHash}, no patches need to be applied.");
                return;
            }

            await MakeReadWriteAsync(new DirectoryInfo(Path.Combine(enginePath, "Engine", "Source", "Programs"))).ConfigureAwait(false);

            foreach (var patchDefinition in _patches)
            {
                var filename = patchDefinition.File.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

                var sourceFile = Path.Combine(enginePath, filename);
                if (File.Exists(sourceFile))
                {
                    var content = await File.ReadAllTextAsync(sourceFile).ConfigureAwait(false);
                    var originalContent = content;
                    for (int i = 0; i < patchDefinition.Patches.Length; i++)
                    {
                        var patch = patchDefinition.Patches[i];
                        if (patch.Mode == "Snip")
                        {
                            if (content.Contains(patch.Contains!, StringComparison.Ordinal))
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Applying...");
                                var startIndex = content.IndexOf(patch.StartIndex!, StringComparison.Ordinal);
                                var endIndex = content.IndexOf(patch.EndIndex!, StringComparison.Ordinal);
                                content = string.Concat(content.AsSpan(0, startIndex), content.AsSpan(endIndex));
                            }
                            else
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Does not apply because contains could not be found: {patch.Contains}");
                            }
                        }
                        else
                        {
                            if (patch.HandleWindowsNewLines)
                            {
                                content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
                            }
                            if (content.Contains(patch.Find!, StringComparison.Ordinal))
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Applying...");
                                content = content.Replace(patch.Find!, patch.Replace!, StringComparison.Ordinal);
                            }
                            else
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Does not apply because content could not be found: {patch.Find}");
                            }
                        }
                    }
                    if (content != originalContent)
                    {
                        await File.WriteAllTextAsync(sourceFile, content).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger.LogTrace($"Patch {filename}: Does not apply because file does not exist: {sourceFile}");
                }
            }

            // Check that we applied the minimum patches correctly.
            var buildGraphFile = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "BuildGraph", "BuildGraph.cs");
            if (!File.Exists(buildGraphFile) || !File.ReadAllText(buildGraphFile).Contains("BUILD_GRAPH_PROJECT_ROOT", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Patching process failed to produce BuildGraph.cs file that contains BUILD_GRAPH_PROJECT_ROOT. Turn on --trace to see logs about the patching process.");
            }

            if (isEngineBuild)
            {
                // When we're doing an engine build, we don't need to rebuild BuildGraph, UBT and associated
                // components because RunUAT.bat/.sh will automatically do it for us. This is also intended
                // to workaround an issue impacting engine builds on macOS where building the projects
                // individually here relies on files that aren't set up yet because RunUAT.sh hasn't
                // fully configured things for the build.
                return;
            }

            await CopyMissingEngineBitsAsync(enginePath).ConfigureAwait(false);

            var epicGamesCoreProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "Shared", "EpicGames.Core", "EpicGames.Core.csproj");
            var epicGamesBuildProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "Shared", "EpicGames.Build", "EpicGames.Build.csproj");
            var unrealBuildToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "UnrealBuildTool", "UnrealBuildTool.csproj");
            var automationToolBuildGraphProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "BuildGraph", "BuildGraph.Automation.csproj");
            var automationToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "AutomationTool.csproj");
            var (msBuildPath, msBuildExtraArgs) = await _msBuildPathResolver.ResolveMSBuildPath().ConfigureAwait(false);
            string? dotnetPath = null;
            var dotnetEnginePath = Path.Combine(enginePath, "Engine", "Binaries", "ThirdParty", "DotNet");
            var dotnetVersionPath = Directory.Exists(dotnetEnginePath) ? Directory.GetDirectories(dotnetEnginePath).First() : null;
            if (dotnetVersionPath != null)
            {
                if (OperatingSystem.IsWindows())
                {
                    dotnetPath = Path.Combine(dotnetVersionPath, "windows", "dotnet.exe");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        dotnetPath = Path.Combine(dotnetVersionPath, "mac-arm64", "dotnet");
                    }
                    else
                    {
                        dotnetPath = Path.Combine(dotnetVersionPath, "mac-x64", "dotnet");
                    }
                }
            }
            if (dotnetPath != null && !Path.Exists(dotnetPath))
            {
                dotnetPath = null;
            }
            if (dotnetPath == null)
            {
                dotnetPath = await _pathResolver.ResolveBinaryPath("dotnet").ConfigureAwait(false);
            }
            if (dotnetPath == null)
            {
                throw new InvalidOperationException("Could not find usable dotnet binary!");
            }
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(
                        dotnetPath,
                        File.GetUnixFileMode(dotnetPath) | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute);
                }
                catch
                {
                }
            }
            _logger.LogInformation($"dotnet being used for patching: {dotnetPath}");

            var sb = new StringBuilder();
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = dotnetPath,
                    Arguments = new LogicalProcessArgument[]
                    {
                        "nuget",
                        "list",
                        "source"
                    }
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(sb),
                CancellationToken.None).ConfigureAwait(false);
            if (!sb.ToString().Contains("https://api.nuget.org/v3/index.json", StringComparison.Ordinal))
            {
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = dotnetPath,
                        Arguments = new LogicalProcessArgument[]
                        {
                            "nuget",
                            "add",
                            "source",
                            "-n",
                            "nuget.org",
                            "https://api.nuget.org/v3/index.json"
                        }
                    },
                    CaptureSpecification.Passthrough,
                    CancellationToken.None).ConfigureAwait(false);
            }

            await using ((await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
            {
                Name = "NuGetPackages"
            }, CancellationToken.None).ConfigureAwait(false)).AsAsyncDisposable(out var nugetStoragePath).ConfigureAwait(false))
            {
                var projects = new[]
                {
                    (name: "EpicGames.Build", path: epicGamesBuildProject),
                    (name: "EpicGames.Core", path: epicGamesCoreProject),
                    (name: "UnrealBuildTool", path: unrealBuildToolProject),
                    (name: "BuildGraph.Automation", path: automationToolBuildGraphProject),
                    (name: "AutomationTool", path: automationToolProject),
                };
                foreach (var project in projects)
                {
                    _logger.LogInformation($"Restoring packages for: {project.name}");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = msBuildPath,
                            Arguments = msBuildExtraArgs.Concat(new LogicalProcessArgument[]
                            {
                                "/nologo",
                                "/verbosity:quiet",
                                project.path,
                                "/property:Configuration=Development",
                                "/property:Platform=AnyCPU",
                                "/p:WarningLevel=0",
                                "/target:Restore",
                                "/p:NuGetAudit=False",
                            }),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NUGET_PACKAGES", nugetStoragePath.Path }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"Failed to rebuild BuildGraph (msbuild restore exited with exit code {exitCode})");
                    }
                }
                foreach (var project in projects)
                {
                    if (project.name == "EpicGames.Core")
                    {
                        continue;
                    }

                    _logger.LogInformation($"Building: {project.name}");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = msBuildPath,
                            Arguments = msBuildExtraArgs.Concat(new LogicalProcessArgument[]
                            {
                                "/nologo",
                                "/verbosity:quiet",
                                project.path,
                                "/property:Configuration=Development",
                                "/property:Platform=AnyCPU",
                                "/p:WarningLevel=0",
                                "/p:NuGetAudit=False",
                            }),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NUGET_PACKAGES", nugetStoragePath.Path }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"Failed to rebuild BuildGraph (msbuild compile exited with exit code {exitCode})");
                    }
                }

                // Test that BuildGraph actually works with the patches.
                _logger.LogInformation($"Testing that patched BuildGraph has correct behaviour...");
                await using ((await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
                {
                    Name = $"BuildGraphPatchTestRoot,{enginePath}",
                }, CancellationToken.None).ConfigureAwait(false)).AsAsyncDisposable(out var buildGraphPatchTestRoot).ConfigureAwait(false))
                {
                    await using ((await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
                    {
                        Name = $"BuildGraphPatchTestSharedStorage,{enginePath}",
                    }, CancellationToken.None).ConfigureAwait(false)).AsAsyncDisposable(out var buildGraphPatchTestSharedStorage).ConfigureAwait(false))
                    {
                        var nodeRoot = Path.Combine(buildGraphPatchTestSharedStorage.Path, "Write and Tag Files");
                        var expectedPath1 = Path.Combine(nodeRoot, "Manifest-BPTTaggedFiles.zip");
                        var expectedPath2 = Path.Combine(nodeRoot, "Manifest-BPTTaggedFiles-00.zip");
                        if (Directory.Exists(nodeRoot))
                        {
                            await DirectoryAsync.DeleteAsync(nodeRoot, true).ConfigureAwait(false);
                        }

                        var environmentVariables = new Dictionary<string, string>
                        {
                            { "IsBuildMachine", "1" },
                            { "uebp_LOCAL_ROOT", enginePath },
                            // BuildGraph in Unreal Engine 5.0 causes input files to be unnecessarily modified. Just allow mutation since I'm not sure what the bug is.
                            { "BUILD_GRAPH_ALLOW_MUTATION", "true" },
                            // Isolate NuGet package restore so that multiple jobs can restore at
                            // the same time.
                            { "NUGET_PACKAGES", nugetStoragePath.Path },
                            { "BUILD_GRAPH_PROJECT_ROOT", buildGraphPatchTestRoot.Path },
                        };

                        var buildGraphScriptPath = Path.GetTempFileName();
                        using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.BuildGraph_TestPatches.xml"))
                        {
                            using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                            {
                                await reader!.CopyToAsync(writer, CancellationToken.None).ConfigureAwait(false);
                            }
                        }

                        try
                        {
                            var exitCode = await _uatExecutor.ExecuteAsync(
                                enginePath,
                                new UATSpecification
                                {
                                    Command = "BuildGraph",
                                    Arguments =
                                    [
                                        "-Target=Use Random Files",
                                        "-noP4",
                                        $"-Script={buildGraphScriptPath}",
                                        $"-SingleNode=Write and Tag Files",
                                        "-WriteToSharedStorage",
                                        $"-SharedStorageDir={buildGraphPatchTestSharedStorage.Path}",
                                        $"-set:OutputDir={buildGraphPatchTestRoot.Path}"
                                    ],
                                    EnvironmentVariables = environmentVariables
                                },
                                CaptureSpecification.Passthrough,
                                CancellationToken.None).ConfigureAwait(false);
                            if (exitCode != 0)
                            {
                                throw new InvalidOperationException($"Failed to rebuild BuildGraph (BuildGraph test execution returned exit code {exitCode})");
                            }
                        }
                        finally
                        {
                            File.Delete(buildGraphScriptPath);
                        }

                        if (!File.Exists(expectedPath1) && !File.Exists(expectedPath2))
                        {
                            throw new InvalidOperationException($"Failed to rebuild BuildGraph (BuildGraph test execution did not emit a file to either '{expectedPath1}' or '{expectedPath2}')");
                        }
                    }
                }
            }

            buildGraphPatchStatus = new BuildGraphPatchStatus
            {
                PatchHash = _patchHash,
                PatchCodeVersion = _patchCodeVersion,
                BuildGraphAutomationDllLastModified = File.Exists(dateCheckPath) ? new DateTimeOffset(File.GetLastWriteTimeUtc(dateCheckPath), TimeSpan.Zero).ToUnixTimeSeconds() : 0,
            };
            File.WriteAllText(patchLevelFilePath, JsonSerializer.Serialize(buildGraphPatchStatus, BuildGraphPatchStatusJsonSerializerContext.Default.BuildGraphPatchStatus));
        }
    }
}