namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;
    using Redpoint.OpenGE.Component.PreprocessorCache.LexerParser;

    internal class OnDiskPreprocessorScanner
    {
        internal const int _cacheVersion = 8;

        public static PreprocessorScanResult ParseIncludes(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
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
