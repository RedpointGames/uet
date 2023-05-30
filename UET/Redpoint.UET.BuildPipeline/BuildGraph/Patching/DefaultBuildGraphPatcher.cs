namespace Redpoint.UET.BuildPipeline.BuildGraph.Patching
{
    using Microsoft.Extensions.Logging;
    using Redpoint.MSBuildResolution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphPatcher : IBuildGraphPatcher
    {
        private readonly ILogger<DefaultBuildGraphPatcher> _logger;
        private readonly IMSBuildPathResolver _msBuildPathResolver;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly BuildGraphPatchSet[] _patches;

        public DefaultBuildGraphPatcher(
            ILogger<DefaultBuildGraphPatcher> logger,
            IMSBuildPathResolver msBuildPathResolver,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _msBuildPathResolver = msBuildPathResolver;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.UET.BuildPipeline.BuildGraph.Patching.BuildGraphPatches.json"))
            {
                _patches = JsonSerializer.Deserialize<BuildGraphPatchSet[]>(stream!, BuildGraphSourceGenerationContext.Default.BuildGraphPatchSetArray)!;
            }
        }

        private async Task MakeReadWriteAsync(DirectoryInfo di)
        {
            foreach (var subdirectory in di.GetDirectories())
            {
                if (subdirectory.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    subdirectory.Attributes = subdirectory.Attributes ^ FileAttributes.ReadOnly;
                    await MakeReadWriteAsync(di);
                }
            }
            foreach (var file in di.GetFiles())
            {
                if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    file.Attributes = file.Attributes ^ FileAttributes.ReadOnly;
                    await MakeReadWriteAsync(di);
                }
            }
        }

        public async Task PatchBuildGraphAsync(string enginePath)
        {
            // @todo: Make this a hash of _patches instead so we don't need to manually modify it.
            const int buildGraphPatchTargetLevel = 1;
            var patchLevelFilePath = Path.Combine(enginePath, "Engine", "Source", "Programs", "UET.BuildGraphPatchLevel.txt");
            var existingBuildGraphPatchLevel = 0;
            if (File.Exists(patchLevelFilePath))
            {
                int.TryParse(File.ReadAllText(patchLevelFilePath).Trim(), out existingBuildGraphPatchLevel);
            }
            if (existingBuildGraphPatchLevel == buildGraphPatchTargetLevel)
            {
                return;
            }

            await MakeReadWriteAsync(new DirectoryInfo(Path.Combine(enginePath, "Engine", "Source", "Programs")));

            foreach (var patchDefinition in _patches)
            {
                var sourceFile = Path.Combine(enginePath, patchDefinition.File);
                if (File.Exists(sourceFile))
                {
                    var content = await File.ReadAllTextAsync(sourceFile);
                    var originalContent = content;
                    foreach (var patch in patchDefinition.Patches)
                    {
                        if (patch.Mode == "Snip")
                        {
                            if (content.Contains(patch.Contains!))
                            {
                                var startIndex = content.IndexOf(patch.StartIndex!);
                                var endIndex = content.IndexOf(patch.EndIndex!);
                                content = content.Substring(0, startIndex) + content.Substring(endIndex);
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
                                content = content.Replace(patch.Find!, patch.Replace!);
                            }
                        }
                    }
                    if (content != originalContent)
                    {
                        await File.WriteAllTextAsync(sourceFile, content);
                    }
                }
            }

            var automationToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "AutomationTool", "BuildGraph", "BuildGraph.Automation.csproj");
            var unrealBuildToolProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "UnrealBuildTool", "UnrealBuildTool.csproj");
            var epicGamesCoreProject = Path.Combine(enginePath, "Engine", "Source", "Programs", "Shared", "EpicGames.Core", "EpicGames.Core.csproj");
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

            var nugetStoragePath = Path.Combine(enginePath, "Engine", "Source", "Programs", ".nuget");
            Directory.CreateDirectory(nugetStoragePath);

            var projects = new[]
            {
                (name: "EpicGames.Core", path: epicGamesCoreProject),
                (name: "BuildGraph.Automation", path: automationToolProject),
                (name: "UnrealBuildTool", path: unrealBuildToolProject),
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
                            { "NUGET_PACKAGES", nugetStoragePath }
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
                            { "NUGET_PACKAGES", nugetStoragePath }
                        }
                    },
                    CaptureSpecification.Passthrough,
                    CancellationToken.None);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to rebuild BuildGraph (msbuild compile exited with exit code {exitCode})");
                }
            }

            File.WriteAllText(patchLevelFilePath, buildGraphPatchTargetLevel.ToString());
        }
    }
}