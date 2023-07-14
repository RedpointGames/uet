namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using Microsoft.AspNetCore.Connections.Features;
    using Microsoft.Extensions.Logging;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    internal class DownloadPluginProjectPrepareProvider : IProjectPrepareProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectPrepareDownloadPlugin>
    {
        private readonly ILogger<DownloadPluginProjectPrepareProvider> _logger;
        private readonly IPhysicalGitCheckout _physicalGitCheckout;

        public DownloadPluginProjectPrepareProvider(
            ILogger<DownloadPluginProjectPrepareProvider> logger,
            IPhysicalGitCheckout physicalGitCheckout)
        {
            _logger = logger;
            _physicalGitCheckout = physicalGitCheckout;
        }

        public string Type => "DownloadPlugin";

        public IRuntimeJson DynamicSettings { get; } = new PrepareProviderRuntimeJson(PrepareProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectPrepareDownloadPlugin;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareDownloadPlugin)x.DynamicSettings))
                .ToList();

            // Attach to the "before compile" steps. We don't need it in any other stages since
            // even if the plugin is used during cooking, the plugin binaries will have been built
            // with the editor.
            foreach (var entry in castedSettings)
            {
                await writer.WriteMacroAsync(
                    new MacroElementProperties
                    {
                        Name = $"DownloadPluginOnCompile-{entry.name}",
                        Arguments = new[]
                        {
                            "TargetType",
                            "TargetName",
                            "TargetPlatform",
                            "TargetConfiguration",
                            "HostPlatform",
                        }
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<
                            DownloadPluginProjectPrepareProvider,
                            BuildConfigProjectDistribution,
                            BuildConfigProjectPrepareDownloadPlugin>(
                            this,
                            context,
                            $"DownloadPlugin.{entry.name}".Replace(" ", "."),
                            entry.settings,
                            new Dictionary<string, string>
                            {
                                { "RepositoryRoot", "$(RepositoryRoot)" },
                                { "ProjectRoot", "$(ProjectRoot)" },
                            });
                    });
                await writer.WritePropertyAsync(
                    new PropertyElementProperties
                    {
                        Name = "DynamicBeforeCompileMacros",
                        Value = $"$(DynamicBeforeCompileMacros)DownloadPluginOnCompile-{entry.name};",
                    });
            }
        }

        public Task RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigProjectPrepareDownloadPlugin)configUnknown;
            var repositoryRoot = runtimeSettings["RepositoryRoot"];
            var projectRoot = Path.GetFullPath(runtimeSettings["ProjectRoot"]);

            BuildConfigProjectPrepareDownloadPluginSource? selectedSource = null;
            string? sensitiveSelectedSourceGitUrl = null;
            var regex = new Regex("\\$\\{([a-zA-Z0-9_-]+)\\}");
            foreach (var source in config.Sources ?? Array.Empty<BuildConfigProjectPrepareDownloadPluginSource>())
            {
                if (source.GitUrl != null)
                {
                    var canSelect = true;
                    foreach (Match match in regex.Matches(source.GitUrl))
                    {
                        var environmentVariable = match.Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable)))
                        {
                            _logger.LogWarning($"Skipping source because the environment variable '{environmentVariable}' is not set: '{source.GitUrl}'");
                            canSelect = false;
                            break;
                        }
                    }
                    if (canSelect)
                    {
                        selectedSource = source;
                        sensitiveSelectedSourceGitUrl = regex.Replace(
                            source.GitUrl,
                            m => Environment.GetEnvironmentVariable(m.Groups[1].Value)!);
                        break;
                    }
                }
                else
                {
                    // We don't know how to handle this source.
                    throw new NotSupportedException();
                }
            }

            if (selectedSource == null)
            {
                _logger.LogError("Exhausted all possible sources for downloading plugin.");
                return 1;
            }

            _logger.LogInformation($"Downloading plugin using the following source: {selectedSource.GitUrl}");

            var pluginFolder = Path.Combine(projectRoot, "Plugins", config.FolderName!);
            Directory.CreateDirectory(pluginFolder);
            _logger.LogInformation($"Checking out plugin into: {pluginFolder}");

            await _physicalGitCheckout.PrepareGitWorkspaceAsync(
                pluginFolder,
                new GitWorkspaceDescriptor
                {
                    RepositoryUrl = sensitiveSelectedSourceGitUrl!,
                    RepositoryCommitOrRef = selectedSource.GitRef!,
                    WorkspaceDisambiguators = Array.Empty<string>(),
                    AdditionalFolderLayers = Array.Empty<string>(),
                    AdditionalFolderZips = Array.Empty<string>(),
                    WindowsSharedGitCachePath = null,
                    MacSharedGitCachePath = null,
                },
                cancellationToken);

            _logger.LogInformation("Plugin has now been checked out.");
            return 0;
        }
    }
}
