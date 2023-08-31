namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Redpoint.OpenGE.Protocol;
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

        public PreprocessorScanResultWithCacheMetadata ParseIncludes(string filePath)
        {
            var st = Stopwatch.StartNew();
            var result = _onDiskPreprocessorScanner.ParseIncludes(filePath);
            return new PreprocessorScanResultWithCacheMetadata
            {
                Result = result,
                CacheStatus = CacheHit.MissDueToOldCacheVersion,
                ResolutionTimeMs = st.ElapsedMilliseconds,
            };
        }
    }
}
