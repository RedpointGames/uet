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

            // Figure out what Android platforms we would need to clean for this job.
            var androidPlatformNames = new HashSet<string>();
            if (components.Contains("Android") ||
                components.Contains("MetaQuest") ||
                components.Contains("GooglePlay"))
            {
                androidPlatformNames.Add("Android");
            }
            if (components.Contains("MetaQuest"))
            {
                androidPlatformNames.Add("MetaQuest");
            }
            if (components.Contains("GooglePlay"))
            {
                androidPlatformNames.Add("GooglePlay");
            }

            // If this job isn't affected by Android platforms, we don't need to do anything.
            if (androidPlatformNames.Count == 0)
            {
                return Task.FromResult(0);
            }

            // We need to clear out 'Binaries/' and 'Saved/StagedBuilds/' of potential stale folders.
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
