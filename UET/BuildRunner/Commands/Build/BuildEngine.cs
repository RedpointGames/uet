namespace BuildRunner.Commands.Build
{
    using BuildRunner.BuildGraph;
    using BuildRunner.BuildGraph.Environment;
    using BuildRunner.Configuration.Engine;
    using BuildRunner.Services;
    using BuildRunner.Workspace;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class BuildEngine : IBuild<BuildConfigEngine>
    {
        private readonly BuildCommand.Options _options;
        private readonly IWorkspaceProvider _workspaceProvider;
        private readonly IScriptExecutor _scriptExecutor;
        private readonly IPathProvider _pathProvider;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly ILogger<BuildEngine> _logger;

        public BuildEngine(
            BuildCommand.Options options,
            IWorkspaceProvider workspaceProvider,
            IScriptExecutor scriptExecutor,
            IPathProvider pathProvider,
            IBuildGraphExecutor buildGraphExecutor,
            ILogger<BuildEngine> logger)
        {
            _options = options;
            _workspaceProvider = workspaceProvider;
            _scriptExecutor = scriptExecutor;
            _pathProvider = pathProvider;
            _buildGraphExecutor = buildGraphExecutor;
            _logger = logger;
        }

        private async Task<BuildGraphSettings> GetBuildGraphSettingsAsync(
            BuildConfigEngineDistribution distribution,
            string enginePath,
            CancellationToken cancellationToken)
        {
            var buildGraphLines = new List<string>();
            var inBuildGraphOutput = false;
            await _scriptExecutor.CapturePowerShellAsync(
                new ScriptSpecification
                {
                    ScriptPath = Path.Combine(_pathProvider.BuildScriptsLib, "Internal_RunUAT.ps1"),
                    Arguments = new[]
                    {
                        "-UATEnginePath",
                        enginePath,
                        "BuildGraph",
                        $@"-Script=""{enginePath}\Engine\Build\InstalledEngineBuild.xml""",
                        "-ListOnly"
                    },
                },
                new CaptureSpecification
                {
                    ReceiveStdout = line =>
                    {
                        if (line.StartsWith("Options:"))
                        {
                            // We are now seeing BuildGraph output.
                            inBuildGraphOutput = true;
                        }
                        if (inBuildGraphOutput)
                        {
                            buildGraphLines.Add(line);
                            return false;
                        }
                        return true;
                    },
                },
                cancellationToken);

            var availablePlatforms = new HashSet<string>();
            var availablePlatformsMac = new HashSet<string>();
            var installedEngineBuild = await File.ReadAllTextAsync(Path.Combine(enginePath, "Engine", "Build", "InstalledEngineBuild.xml"));
            foreach (var lineRaw in buildGraphLines)
            {
                var line = lineRaw.Trim();
                if (line.StartsWith("-set:With") &&
                    !line.StartsWith("-set:WithDDC") &&
                    !line.StartsWith("-set:WithClient") &&
                    !line.StartsWith("-set:WithServer") &&
                    !line.StartsWith("-set:WithFullDebugInfo"))
                {
                    line = line.Substring("-set:With".Length);
                    line = line.Split("=")[0];
                    availablePlatforms.Add(line);
                    if (installedEngineBuild.Contains($"<Option Name=\"With{line}\""))
                    {
                        // macOS only knows about public (non-console) platforms.
                        availablePlatformsMac.Add(line);
                    }
                }
            }

            var platforms = availablePlatforms.ToDictionary(k => k, v => false);
            var platformsMac = availablePlatformsMac.ToDictionary(k => k, v => false);
            foreach (var platform in distribution.Build.Platforms)
            {
                if (platforms.ContainsKey(platform))
                {
                    platforms[platform] = true;
                }
                if (platformsMac.ContainsKey(platform))
                {
                    platformsMac[platform] = true;
                }
            }

            var windowsSettings = new Dictionary<string, string>
            {
                // Target types
                { "WithClient", distribution.Build.TargetTypes.Contains("Client") ? "true" : "false" },
                { "WithServer", distribution.Build.TargetTypes.Contains("Server") ? "true" : "false" },

                // Cook options
                { "WithDDC", distribution.Cook.GenerateDDC ? "true" : "false" },
            };
            foreach (var kv in platforms)
            {
                windowsSettings[$"With{kv.Key}"] = kv.Value ? "true" : "false";
            }
            var macSettings = new Dictionary<string, string>
            {
                // Target types
                { "WithClient", distribution.Build.TargetTypes.Contains("Client") ? "true" : "false" },
                { "WithServer", distribution.Build.TargetTypes.Contains("Server") ? "true" : "false" },

                // Cook options
                { "WithDDC", distribution.Cook.GenerateDDC ? "true" : "false" },
            };
            foreach (var kv in platformsMac)
            {
                windowsSettings[$"With{kv.Key}"] = kv.Value ? "true" : "false";
            }

            return new BuildGraphSettings
            {
                WindowsSettings = windowsSettings,
                MacSettings = macSettings,
            };
        }

        public async Task<int> ExecuteAsync(InvocationContext context, BuildConfigEngine config)
        {
            var distributionName = context.ParseResult.GetValueForOption(_options.Distribution);
            var distribution = config.Distributions.First(x => x.Name == distributionName);

            await using (var workspace = await _workspaceProvider.GetGitWorkspaceAsync(
                distribution.Source.Repository,
                distribution.Source.Commit,
                distribution.Build.ConsoleDirectories,
                "E",
                context.GetCancellationToken()))
            {
                // Patch BuildGraph. We do this before computing settings, since BuildGraphSettings_Engine.ps1
                // will invoke BuildGraph to detect all of the available platforms.
                var exitCode = await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = Path.Combine(_pathProvider.BuildScriptsLib, "Patch_BuildGraph.ps1"),
                        Arguments = new[]
                        {
                            "-EnginePath",
                            workspace.Path
                        },
                    },
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    throw new Exception($"Failed to patch BuildGraph (got exit code: {exitCode})");
                }

                // Compute the BuildGraph settings.
                var settings = await GetBuildGraphSettingsAsync(
                    distribution,
                    workspace.Path,
                    CancellationToken.None);

                // BuildGraph in Unreal Engine 5.0 causes input files to be unnecessarily modified. Just allow mutation since I'm not sure what the bug is.
                Environment.SetEnvironmentVariable("BUILD_GRAPH_ALLOW_MUTATION", "true");

                // @todo: Kill AutomationToolLauncher.exe ?

                // Execute BuildGraph.
                var buildGraphArgs = new List<string>();
                foreach (var kv in settings.WindowsSettings)
                {
                    var value = kv.Value.Replace("__REPOSITORY_ROOT__", _pathProvider.RepositoryRoot);
                    value = value.Replace("/", "\\");
                    value = value.TrimEnd('\\');
                    buildGraphArgs.Add($@"-set__{kv.Key}={value}");
                }
                foreach (var arg in buildGraphArgs)
                {
                    _logger.LogInformation(arg);
                }
                return await _buildGraphExecutor.ExecuteGraphAsync(
                    workspace.Path,
                    $@"{workspace.Path}\Engine\Build\InstalledEngineBuild.xml",
                    "Make Installed Build Win64",
                    buildGraphArgs,
                    context.GetCancellationToken());
            }
        }
    }
}
