namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <remarks>
    /// This is just used for unit testing the resolver.
    /// </remarks>
    internal class NonCachingPreprocessorScanner : ICachingPreprocessorScanner, IDisposable
    {
        private readonly OnDiskPreprocessorScanner _onDiskPreprocessorScanner;

        public NonCachingPreprocessorScanner()
        {
            _onDiskPreprocessorScanner = new OnDiskPreprocessorScanner();
        }

        public void Dispose()
        {
        }

        public async Task<PreprocessorScanResultWithCacheMetadata> ParseIncludes(
            string filePath, 
            CancellationToken cancellationToken)
        {
            var st = Stopwatch.StartNew();
            var result = await _onDiskPreprocessorScanner.ParseIncludes(
                filePath,
                cancellationToken);
            return new PreprocessorScanResultWithCacheMetadata
            {
                Result = result,
                CacheStatus = CacheHit.MissDueToOldCacheVersion,
                ResolutionTimeMs = (long)st.ElapsedMilliseconds,
            };
        }
    }
}
