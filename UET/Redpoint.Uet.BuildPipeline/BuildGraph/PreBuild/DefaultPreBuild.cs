namespace Redpoint.Uet.BuildPipeline.BuildGraph.PreBuild
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultPreBuild : IPreBuild
    {
        private readonly ILogger<DefaultPreBuild> _logger;

        public DefaultPreBuild(
            ILogger<DefaultPreBuild> logger)
        {
            _logger = logger;
        }

        public Task<int> RunGeneralPreBuild(
            string repositoryRoot,
            string nodeName,
            IReadOnlyDictionary<string, string> preBuildGraphArguments,
            CancellationToken cancellationToken)
        {
            if (!preBuildGraphArguments.TryGetValue("ProjectRoot", out var projectRoot))
            {
                // Not a project.
                return Task.FromResult(0);
            }

            var components = nodeName.Split(' ');
            if (!components.Contains("MetaQuest") &&
                !components.Contains("GooglePlay"))
            {
                // Not a node that would be affected by stale Android files.
                return Task.FromResult(0);
            }
            var platformName = components.Contains("MetaQuest") ? "MetaQuest" : "GooglePlay";

            // We need to clear out 'Binaries/' and 'Saved/StagedBuilds/' of potential stale folders.
            var androidPlatformNames = new[] { "Android", platformName };
            var binariesFolder = new DirectoryInfo(Path.Combine(projectRoot, "Binaries"));
            var stagedBuildsFolder = new DirectoryInfo(Path.Combine(projectRoot, "Saved", "StagedBuilds"));
            if (binariesFolder.Exists)
            {
                foreach (var binaryFolder in binariesFolder.GetDirectories())
                {
                    if (androidPlatformNames.Any(x => binaryFolder.Name == x || binaryFolder.Name.StartsWith($"{x}_", StringComparison.Ordinal)))
                    {
                        _logger.LogInformation($"Deleting '{binaryFolder.FullName}' since it can interfere with this build job.");
                        binaryFolder.Delete(true);
                    }
                }
            }
            if (stagedBuildsFolder.Exists)
            {
                foreach (var stagedBuildFolder in stagedBuildsFolder.GetDirectories())
                {
                    if (androidPlatformNames.Any(x => stagedBuildFolder.Name == x || stagedBuildFolder.Name.StartsWith($"{x}_", StringComparison.Ordinal)))
                    {
                        _logger.LogInformation($"Deleting '{stagedBuildFolder.FullName}' since it can interfere with this build job.");
                        stagedBuildFolder.Delete(true);
                    }
                }
                foreach (var obbFile in stagedBuildsFolder.GetFiles("*.obb"))
                {
                    _logger.LogInformation($"Deleting '{obbFile.FullName}' since it can interfere with this build job.");
                    obbFile.Delete();
                }
            }
            return Task.FromResult(0);
        }
    }
}
