namespace Redpoint.OpenGE.PreprocessorCache
{
    using PreprocessorCacheApi;
    using Redpoint.OpenGE.PreprocessorCache.LexerParser;
    using System;
    using System.Threading.Tasks;

    internal class OnDiskPreprocessorScanner
    {
        internal const int _cacheVersion = 1;

        public async Task<PreprocessorScanResult> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            DateTimeOffset ticks = File.GetLastWriteTimeUtc(filePath);
            var directives = PreprocessorScanner.Scan(lines);
            var result = new PreprocessorScanResult
            {
                FileLastWriteTicks = ticks.UtcTicks,
                CacheVersion = _cacheVersion,
            };
            result.Directives.AddRange(directives);
            return result;
        }
    }
}
