namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Exceptions;
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
            var factory = new ZoneTreeFactory<string, PreprocessorScanResult>()
                .SetDataDirectory(dataDirectory)
                .SetKeySerializer(new Utf8StringSerializer())
                .SetValueSerializer(new ProtobufZoneTreeSerializer<PreprocessorScanResult>())
                .ConfigureWriteAheadLogOptions(configure =>
                {
                    // @note: If we ever run the preprocessor scanner without clean shutdown,
                    // we should set the following option:
                    //
                    //configure.WriteAheadLogMode = WriteAheadLogMode.Sync;
                });
            try
            {
                _disk = factory.OpenOrCreate();
            }
            catch (WriteAheadLogCorruptionException)
            {
                if (Directory.Exists(dataDirectory))
                {
                    try
                    {
                        Directory.Delete(dataDirectory, true);
                    }
                    catch { }
                }
                _disk = factory.Create();
            }
            _logger = logger;
            _onDiskPreprocessorScanner = onDiskPreprocessorScanner;
        }

        public void Dispose()
        {
            _disk.Dispose();
        }

        public async Task<PreprocessorScanResultWithCacheMetadata> ParseIncludes(string filePath, CancellationToken cancellationToken)
        {
            var st = Stopwatch.StartNew();

            CacheHit cacheStatus;
            if (_disk.TryGet(filePath, out var diskCachedValue))
            {
                if (diskCachedValue.CacheVersion == OnDiskPreprocessorScanner._cacheVersion)
                {
                    var currentTicks = ((DateTimeOffset)File.GetLastWriteTimeUtc(filePath)).UtcTicks;
                    var lastTicks = diskCachedValue.FileLastWriteTicks;
                    if (currentTicks <= lastTicks)
                    {
                        return new PreprocessorScanResultWithCacheMetadata
                        {
                            Result = diskCachedValue!,
                            CacheStatus = CacheHit.Hit,
                            ResolutionTimeMs = st.ElapsedMilliseconds,
                        };
                    }
                    else
                    {
                        cacheStatus = CacheHit.MissDueToFileOutOfDate;
                    }
                }
                else
                {
                    cacheStatus = CacheHit.MissDueToOldCacheVersion;
                }
            }
            else
            {
                cacheStatus = CacheHit.MissDueToMissingFile;
            }

            var freshValue = await _onDiskPreprocessorScanner.ParseIncludes(
                filePath,
                cancellationToken);
            _disk.Upsert(filePath, freshValue);
            return new PreprocessorScanResultWithCacheMetadata
            {
                Result = freshValue,
                CacheStatus = cacheStatus,
                ResolutionTimeMs = st.ElapsedMilliseconds,
            };
        }
    }
}
