namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using Microsoft.Extensions.Logging;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class DownloadPluginProjectPrepareProvider
        : IProjectPrepareProvider
        , IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectPrepareDownloadPlugin>
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

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries)
        {
            // We don't run this inside BuildGraph.
            return Task.CompletedTask;
        }

        public async Task<int> RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            IReadOnlyDictionary<string, string> preBuildGraphArguments,
            CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareDownloadPlugin)x.DynamicSettings))
                .ToList();

            var projectRoot = Path.GetFullPath(preBuildGraphArguments["ProjectRoot"]);

            foreach (var entry in castedSettings)
            {
                var config = entry.settings;

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
                        RepositoryBranchForReservationParameters = selectedSource.GitRef!,
                        AdditionalFolderLayers = Array.Empty<string>(),
                        AdditionalFolderZips = Array.Empty<string>(),
                        WindowsSharedGitCachePath = null,
                        MacSharedGitCachePath = null,
                    },
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Plugin has now been checked out.");
            }

            return 0;
        }

        public Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
