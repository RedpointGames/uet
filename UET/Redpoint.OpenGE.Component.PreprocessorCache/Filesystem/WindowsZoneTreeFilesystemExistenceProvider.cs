namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System.Collections.Concurrent;
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
    internal class WindowsZoneTreeFilesystemExistenceProvider : IFilesystemExistenceProvider, IAsyncDisposable, IRefComparer<string>, ISerializer<string>
    {
        private readonly IZoneTree<string, FilesystemExistenceEntry>? _disk;
        private readonly ConcurrentDictionary<string, (bool exists, long lastCheckTicks)>? _inMemoryFallback;
        private readonly IReservation? _reservation;

        public WindowsZoneTreeFilesystemExistenceProvider(
            IOpenGECacheReservationManagerProvider openGEReservationManagerProvider)
        {
            _reservation = openGEReservationManagerProvider.ReservationManager.TryReserveExact("Filesystem");
            if (_reservation == null)
            {
                _inMemoryFallback = new ConcurrentDictionary<string, (bool, long)>(StringComparer.InvariantCultureIgnoreCase);
                return;
            }

            var factory = new ZoneTreeFactory<string, FilesystemExistenceEntry>()
                .SetDataDirectory(_reservation.ReservedPath)
                .SetComparer(this)
                .SetKeySerializer(this)
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool FileExistsInternal(string path)
        {
            var attrs = GetFileAttributes(path);
            return (attrs != unchecked((uint)-1) && (attrs & 0x00000010) == 0);
        }

        public bool FileExists(string path, long buildStartTicks)
        {
            if (_inMemoryFallback != null)
            {
                var e = _inMemoryFallback.GetOrAdd(path, x => (FileExistsInternal(x), buildStartTicks));
                if (e.lastCheckTicks < buildStartTicks)
                {
                    var val = (FileExistsInternal(path), buildStartTicks);
                    return _inMemoryFallback.AddOrUpdate(
                        path,
                        val,
                        (_, _) => val).exists;
                }
                return e.exists;
            }

            if (!_disk!.TryGet(path, out var value) ||
                value.LastCheckTicks < buildStartTicks)
            {
                var exists = FileExistsInternal(path);
                _disk.Upsert(path, new FilesystemExistenceEntry { Exists = exists, LastCheckTicks = buildStartTicks });
                return exists;
            }
            else
            {
                return value.Exists;
            }
        }

        public void Dispose()
        {
        }

        int IRefComparer<string>.Compare(in string x, in string y)
        {
            return string.Compare(x, y, StringComparison.InvariantCultureIgnoreCase);
        }

        string ISerializer<string>.Deserialize(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        byte[] ISerializer<string>.Serialize(in string entry)
        {
            return Encoding.UTF8.GetBytes(entry.ToLowerInvariant());
        }

        public async ValueTask DisposeAsync()
        {
            _disk?.Dispose();
            if (_reservation != null)
            {
                await _reservation.DisposeAsync();
            }
        }
    }
}
