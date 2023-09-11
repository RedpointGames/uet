namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO.Hashing;
    using System.Runtime.CompilerServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Comparers;
    using Tenray.ZoneTree.Exceptions;
    using Tenray.ZoneTree.Serializers;
    using static Windows.Win32.PInvoke;

    [SupportedOSPlatform("windows5.2")]
    internal class WindowsZoneTreeFilesystemExistenceProvider : IFilesystemExistenceProvider, IAsyncDisposable, IRefComparer<long>
    {
        private readonly IZoneTree<long, FilesystemExistenceEntry>? _disk;
        private readonly ConcurrentDictionary<long, (bool exists, long lastCheckTicks)>? _inMemoryFallback;
        private readonly IReservation? _reservation;

        public WindowsZoneTreeFilesystemExistenceProvider(
            ILogger<WindowsZoneTreeFilesystemExistenceProvider> logger,
            IReservationManagerForOpenGE openGEReservationManagerProvider)
        {
            _reservation = openGEReservationManagerProvider.ReservationManager.TryReserveExact("Filesystem");
            if (_reservation == null)
            {
                _inMemoryFallback = new ConcurrentDictionary<long, (bool, long)>();
                return;
            }

            var st = Stopwatch.StartNew();
            var factory = new ZoneTreeFactory<long, FilesystemExistenceEntry>()
                .SetDataDirectory(_reservation.ReservedPath)
                .SetComparer(this)
                .SetDiskSegmentCompression(false)
                .SetValueSerializer(new ProtobufZoneTreeSerializer<FilesystemExistenceEntry>());
            try
            {
                _disk = factory.OpenOrCreate();
            }
            catch (WriteAheadLogCorruptionException)
            {
                if (Directory.Exists(_reservation.ReservedPath))
                {
                    try
                    {
                        Directory.Delete(_reservation.ReservedPath, true);
                    }
                    catch { }
                }
                _disk = factory.Create();
            }
            catch (TreeComparerMismatchException)
            {
                if (Directory.Exists(_reservation.ReservedPath))
                {
                    try
                    {
                        Directory.Delete(_reservation.ReservedPath, true);
                    }
                    catch { }
                }
                _disk = factory.Create();
            }
            logger.LogTrace($"Filesystem existence cache initialized in {st.Elapsed.TotalSeconds:F2} seconds.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool FileExistsInternal(string path)
        {
            var attrs = GetFileAttributes(path);
            return (attrs != unchecked((uint)-1) && (attrs & 0x00000010) == 0);
        }

        public bool FileExists(string path, long buildStartTicks)
        {
            var pathHash = XxHash64Helpers.HashString(path.ToLowerInvariant()).hash;

            if (_inMemoryFallback != null)
            {
                var e = _inMemoryFallback.GetOrAdd(pathHash, x => (FileExistsInternal(path), buildStartTicks));
                if (e.lastCheckTicks < buildStartTicks)
                {
                    var val = (FileExistsInternal(path), buildStartTicks);
                    return _inMemoryFallback.AddOrUpdate(
                        pathHash,
                        val,
                        (_, _) => val).exists;
                }
                return e.exists;
            }

            if (!_disk!.TryGet(pathHash, out var value) ||
                value.LastCheckTicks < buildStartTicks)
            {
                var exists = FileExistsInternal(path);
                _disk.Upsert(pathHash, new FilesystemExistenceEntry { Exists = exists, LastCheckTicks = buildStartTicks });
                return exists;
            }
            else
            {
                return value.Exists;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disk?.Dispose();
            if (_reservation != null)
            {
                await _reservation.DisposeAsync().ConfigureAwait(false);
            }
        }

        int IRefComparer<long>.Compare(in long x, in long y)
        {
            return x.CompareTo(y);
        }
    }
}
