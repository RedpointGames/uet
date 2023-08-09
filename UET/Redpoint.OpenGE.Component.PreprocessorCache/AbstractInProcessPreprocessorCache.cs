namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class AbstractInProcessPreprocessorCache : PreprocessorCacheApi.PreprocessorCacheApiBase, IPreprocessorCache,
        IAsyncDisposable
    {
        internal AbstractInProcessPreprocessorCache()
        {
        }

        public abstract DateTimeOffset LastGrpcRequestUtc { get; protected set; }

        public abstract Task EnsureAsync();

        public abstract ValueTask DisposeAsync();

        public abstract Task<PreprocessorResolutionResultWithTimingMetadata> GetResolvedDependenciesAsync(
            string filePath,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            Dictionary<string, string> globalDefinitions,
            long buildStartTicks,
            CancellationToken cancellationToken);

        public abstract Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken);
    }
}
