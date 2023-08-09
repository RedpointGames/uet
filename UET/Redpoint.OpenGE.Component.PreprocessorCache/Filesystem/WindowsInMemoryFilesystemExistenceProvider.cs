namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using System.Collections.Concurrent;
    using System.Runtime.CompilerServices;
    using System.Runtime.Versioning;
    using static Windows.Win32.PInvoke;

    [SupportedOSPlatform("windows5.2")]
    internal class WindowsInMemoryFilesystemExistenceProvider : IFilesystemExistenceProvider
    {
        private ConcurrentDictionary<string, (bool exists, long lastCheckTicks)> _existenceCache = new ConcurrentDictionary<string, (bool, long)>(StringComparer.InvariantCultureIgnoreCase);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool FileExistsInternal(string path)
        {
            var attrs = GetFileAttributes(path);
            return (attrs != unchecked((uint)-1) && (attrs & 0x00000010) == 0);
        }

        public bool FileExists(string path, long buildStartTicks)
        {
            var e = _existenceCache.GetOrAdd(path, x => (FileExistsInternal(x), buildStartTicks));
            if (e.lastCheckTicks < buildStartTicks)
            {
                // We always re-check files that are reported to exist.
                var val = (FileExistsInternal(path), buildStartTicks);
                return _existenceCache.AddOrUpdate(
                    path,
                    val,
                    (_, _) => val).exists;
            }
            return e.exists;
        }
    }
}
