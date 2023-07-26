namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using Microsoft.Extensions.Logging;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Reflection;
    using System.Security.Cryptography;
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
        private readonly BuildGraphPatchSet[] _patches;
        private readonly string _patchHash;

        public DefaultBuildGraphPatcher(
            ILogger<DefaultBuildGraphPatcher> logger,
            IMSBuildPathResolver msBuildPathResolver,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IDynamicWorkspaceProvider dynamicWorkspaceProvider)
        {
            _logger = logger;
            _msBuildPathResolver = msBuildPathResolver;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.Patching.BuildGraphPatches.json"))
            {
                _patches = JsonSerializer.Deserialize<BuildGraphPatchSet[]>(stream!, BuildGraphSourceGenerationContext.Default.BuildGraphPatchSetArray)!;
                stream!.Seek(0, SeekOrigin.Begin);
                using (var sha = SHA1.Create())
                {
                    _patchHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private async Task MakeReadWriteAsync(DirectoryInfo di)
        {
            foreach (var subdirectory in di.GetDirectories())
            {
                if (subdirectory.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    subdirectory.Attributes = subdirectory.Attributes ^ FileAttributes.ReadOnly;
                }
                await MakeReadWriteAsync(subdirectory);
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
                    if (source.StartsWith("stream:"))
                    {
                        sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(source.Substring("stream:".Length))!;
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
                            await sourceStream.CopyToAsync(targetStream);
                        }
                    }
                }
            }
        }

        public async Task PatchBuildGraphAsync(string enginePath, bool isEngineBuild)
        {
            var patchLevelFilePath = Path.Combine(enginePath, "Engine", "Source", "Programs", "UET.BuildGraphPatchLevel.txt");
            var existingBuildGraphPatchLevel = string.Empty;
            if (File.Exists(patchLevelFilePath))
            {
                existingBuildGraphPatchLevel = File.ReadAllText(patchLevelFilePath).Trim();
            }
            if (existingBuildGraphPatchLevel == _patchHash)
            {
                _logger.LogInformation($"BuildGraph patch version is already {_patchHash}, no patches need to be applied.");
                return;
            }

            _logger.LogInformation($"BuildGraph patch version is {existingBuildGraphPatchLevel}, but the target patch version is {_patchHash}, applying patches...");

            await MakeReadWriteAsync(new DirectoryInfo(Path.Combine(enginePath, "Engine", "Source", "Programs")));

            foreach (var patchDefinition in _patches)
            {
                var filename = patchDefinition.File.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

                var sourceFile = Path.Combine(enginePath, filename);
                if (File.Exists(sourceFile))
                {
                    var content = await File.ReadAllTextAsync(sourceFile);
                    var originalContent = content;
                    for (int i = 0; i < patchDefinition.Patches.Length; i++)
                    {
                        var patch = patchDefinition.Patches[i];
                        if (patch.Mode == "Snip")
                        {
                            if (content.Contains(patch.Contains!))
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Applying...");
                                var startIndex = content.IndexOf(patch.StartIndex!);
                                var endIndex = content.IndexOf(patch.EndIndex!);
                                content = content.Substring(0, startIndex) + content.Substring(endIndex);
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
                                content = content.Replace("\r\n", "\n");
                            }
                            if (content.Contains(patch.Find!))
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Applying...");
                                content = content.Replace(patch.Find!, patch.Replace!);
                            }
                            else
                            {
                                _logger.LogTrace($"Patch {filename} #{i}: Does not apply because content could not be found: {patch.Find}");
                            }
                        }
                    }
                    if (content != originalContent)
                    {
                        await File.WriteAllTextAsync(sourceFile, content);
                    }
                }
                else
                {
                    _logger.LogTrace($"Patch {filename}: Does not apply because file does not exist: {sourceFile}");
                }
            }

            // Check that we applied the minimum patches correctly.
            var buildGraphFile = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "BuildGraph", "BuildGraph.cs");
            if (!File.Exists(buildGraphFile) || !File.ReadAllText(buildGraphFile).Contains("BUILD_GRAPH_PROJECT_ROOT"))
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

            await CopyMissingEngineBitsAsync(enginePath);

            var epicGamesCoreProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "Shared", "EpicGames.Core", "EpicGames.Core.csproj");
            var epicGamesBuildProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "Shared", "EpicGames.Build", "EpicGames.Build.csproj");
            var unrealBuildToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "UnrealBuildTool", "UnrealBuildTool.csproj");
            var automationToolBuildGraphProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "BuildGraph", "BuildGraph.Automation.csproj");
            var automationToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "AutomationTool.csproj");
            var (msBuildPath, msBuildExtraArgs) = await _msBuildPathResolver.ResolveMSBuildPath();
            var dotnetPath = await _pathResolver.ResolveBinaryPath("dotnet");

            var sb = new StringBuilder();
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = dotnetPath,
                    Arguments = new[]
                    {
                        "nuget",
                        "list",
                        "source"
                    }
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(sb),
                CancellationToken.None);
            if (!sb.ToString().Contains("https://api.nuget.org/v3/index.json"))
            {
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = dotnetPath,
                        Arguments = new[]
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
                    CancellationToken.None);
            }

            await using (var nugetStoragePath = await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
            {
                Name = "NuGetPackages"
            }, CancellationToken.None))
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
                            Arguments = msBuildExtraArgs.Concat(new[]
                            {
                                "/nologo",
                                "/verbosity:quiet",
                                project.path,
                                "/property:Configuration=Development",
                                "/property:Platform=AnyCPU",
                                "/p:WarningLevel=0",
                                "/target:Restore"
                            }),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NUGET_PACKAGES", nugetStoragePath.Path }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None);
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
                            Arguments = msBuildExtraArgs.Concat(new[]
                            {
                                "/nologo",
                                "/verbosity:quiet",
                                project.path,
                                "/property:Configuration=Development",
                                "/property:Platform=AnyCPU",
                                "/p:WarningLevel=0"
                            }),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NUGET_PACKAGES", nugetStoragePath.Path }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"Failed to rebuild BuildGraph (msbuild compile exited with exit code {exitCode})");
                    }
                }
            }

            File.WriteAllText(patchLevelFilePath, _patchHash);
        }
    }
}