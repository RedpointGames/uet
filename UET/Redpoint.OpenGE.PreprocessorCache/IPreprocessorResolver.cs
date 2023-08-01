namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPreprocessorResolver
    {
        Task<PreprocessorResolutionResultWithTimingMetadata> ResolveAsync(
            ICachingPreprocessorScanner scanner,
            string path,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            string[] systemDirectories,
            Dictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken);
    }
}
