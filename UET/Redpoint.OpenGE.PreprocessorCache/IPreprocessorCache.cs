﻿namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using System.Threading.Tasks;

    public interface IPreprocessorCache
    {
        Task EnsureConnectedAsync();

        Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken);

        Task<PreprocessorResolutionResultWithTimingMetadata> GetResolvedDependenciesAsync(
            string filePath,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            string[] systemDirectories,
            Dictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken);
    }
}
