namespace Redpoint.OpenGE.Component.PreprocessorCache.DependencyResolution
{
    using Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IPreprocessorResolver
    {
        Task<PreprocessorResolutionResultWithTimingMetadata> ResolveAsync(
            ICachingPreprocessorScanner scanner,
            string path,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            Dictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken);
    }
}
