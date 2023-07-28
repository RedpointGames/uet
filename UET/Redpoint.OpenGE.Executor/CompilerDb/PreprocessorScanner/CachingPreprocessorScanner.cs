namespace Redpoint.OpenGE.Executor.CompilerDb.PreprocessorScanner
{
    using Redpoint.Concurrency;
    using System;
    using System.Threading.Tasks;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Options;
    using Tenray.ZoneTree.Serializers;

    internal class CachingPreprocessorScanner : IPreprocessorScanner
    {
        private readonly IZoneTree<string, PreprocessorScanResult> _disk;
        private readonly AtomicConcurrentDictionary<string, PreprocessorScanResult> _inmemory;
        private readonly OnDiskPreprocessorScanner _onDiskPreprocessorScanner;

        public CachingPreprocessorScanner(
            OnDiskPreprocessorScanner onDiskPreprocessorScanner)
        {
            _disk = new ZoneTreeFactory<string, PreprocessorScanResult>()
                .SetDataDirectory(@"C:\ProgramData\UET\CompilerDb\PreprocessorScans")
                .SetKeySerializer(new Utf8StringSerializer())
                .SetValueSerializer(new PreprocessorScanResultSerializer())
                .ConfigureWriteAheadLogOptions(configure =>
                {
                    // @todo: This can be faster if we have a clear exit callback.
                    configure.WriteAheadLogMode = WriteAheadLogMode.Sync;
                })
                .OpenOrCreate();
            _inmemory = new AtomicConcurrentDictionary<string, PreprocessorScanResult>(
                OperatingSystem.IsWindows() ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture);
            _onDiskPreprocessorScanner = onDiskPreprocessorScanner;
        }

        public async Task<PreprocessorScanResult> ParseIncludes(string filePath, CancellationToken cancellationToken)
        {
            return await _inmemory.AtomicAddOrWaitAsync(
                filePath,
                async ct =>
                {
                    if (_disk.TryGet(filePath, out var diskCachedValue) ||
                        ((DateTimeOffset)File.GetLastWriteTimeUtc(filePath)).UtcTicks > diskCachedValue.FileLastWriteTicks)
                    {
                        return diskCachedValue!;
                    }

                    var freshValue = await _onDiskPreprocessorScanner.ParseIncludes(
                        filePath,
                        ct);
                    _disk.Upsert(filePath, freshValue);
                    return freshValue;
                },
                cancellationToken);
        }
    }
}
