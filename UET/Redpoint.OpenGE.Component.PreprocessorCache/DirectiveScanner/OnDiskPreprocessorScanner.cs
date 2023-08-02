namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;
    using Redpoint.OpenGE.Component.PreprocessorCache.LexerParser;

    internal class OnDiskPreprocessorScanner
    {
        internal const int _cacheVersion = 7;

        public async Task<PreprocessorScanResult> ParseIncludes(
            string filePath,
            CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            DateTimeOffset ticks = File.GetLastWriteTimeUtc(filePath);
            var directivesAndConditions = PreprocessorScanner.Scan(lines);
            var result = new PreprocessorScanResult
            {
                FileLastWriteTicks = ticks.UtcTicks,
                CacheVersion = _cacheVersion,
            };
            result.Conditions.AddRange(directivesAndConditions.Conditions.Values);
            result.Directives.AddRange(directivesAndConditions.Directives);
            return result;
        }
    }
}
