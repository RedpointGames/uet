namespace Redpoint.OpenGE.PreprocessorCache
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Serializers;

    internal class CachingPreprocessorScanner : ICachingPreprocessorScanner, IDisposable
    {
        private readonly IZoneTree<string, PreprocessorScanResult> _disk;
        private readonly ILogger<CachingPreprocessorScanner> _logger;
        private readonly OnDiskPreprocessorScanner _onDiskPreprocessorScanner;

        public CachingPreprocessorScanner(
            ILogger<CachingPreprocessorScanner> logger,
            OnDiskPreprocessorScanner onDiskPreprocessorScanner,
            string dataDirectory)
        {
            _disk = new ZoneTreeFactory<string, PreprocessorScanResult>()
                .SetDataDirectory(dataDirectory)
                .SetKeySerializer(new Utf8StringSerializer())
                .SetValueSerializer(new PreprocessorScanResultSerializer())
                .ConfigureWriteAheadLogOptions(configure =>
                {
                    // @note: If we ever run the preprocessor scanner without clean shutdown,
                    // we should set the following option:
                    //
                    //configure.WriteAheadLogMode = WriteAheadLogMode.Sync;
                })
                .OpenOrCreate();
            _logger = logger;
            _onDiskPreprocessorScanner = onDiskPreprocessorScanner;
        }

        public void Dispose()
        {
            _disk.Dispose();
        }

        public async Task<PreprocessorScanResultWithCacheInfo> ParseIncludes(string filePath, CancellationToken cancellationToken)
        {
            var st = Stopwatch.StartNew();

            PreprocessorCacheApi.CacheHit cacheStatus;
            if (_disk.TryGet(filePath, out var diskCachedValue))
            {
                var currentTicks = ((DateTimeOffset)File.GetLastWriteTimeUtc(filePath)).UtcTicks;
                var lastTicks = diskCachedValue.FileLastWriteTicks;
                if (currentTicks <= lastTicks)
                {
                    return new PreprocessorScanResultWithCacheInfo
                    {
                        ScanResult = diskCachedValue!,
                        CacheStatus = PreprocessorCacheApi.CacheHit.Hit,
                        ResolutionTimeMs = (long)st.ElapsedMilliseconds,
                    };
                }
                else
                {
                    cacheStatus = PreprocessorCacheApi.CacheHit.MissDueToFileOutOfDate;
                }
            }
            else
            {
                cacheStatus = PreprocessorCacheApi.CacheHit.MissDueToMissingFile;
            }

            var freshValue = await _onDiskPreprocessorScanner.ParseIncludes(
                filePath,
                cancellationToken);
            _disk.Upsert(filePath, freshValue);
            return new PreprocessorScanResultWithCacheInfo
            {
                ScanResult = freshValue,
                CacheStatus = cacheStatus,
                ResolutionTimeMs = (long)st.ElapsedMilliseconds,
            };
        }
    }
}
