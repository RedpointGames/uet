namespace Redpoint.OpenGE.PreprocessorCache
{
    public class PreprocessorScanResultWithCacheInfo
    {
        public required PreprocessorScanResult ScanResult { get; set; }
        public long ResolutionTimeMs { get; set; }
        public PreprocessorCacheApi.CacheHit CacheStatus { get; set; }
    }
}
