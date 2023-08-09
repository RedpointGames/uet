namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using System.Collections.Concurrent;

    internal class GenericInMemoryFilesystemExistenceProvider : IFilesystemExistenceProvider
    {
        private static readonly StringComparer _pathComparison = OperatingSystem.IsWindows()
            ? StringComparer.InvariantCultureIgnoreCase
            : StringComparer.InvariantCulture;

        private ConcurrentDictionary<string, (bool exists, long lastCheckTicks)> _existenceCache = new ConcurrentDictionary<string, (bool, long)>(_pathComparison);

        public bool FileExists(string path, long buildStartTicks)
        {
            var e = _existenceCache.GetOrAdd(path, x => (File.Exists(x), buildStartTicks));
            if (e.lastCheckTicks < buildStartTicks)
            {
                // We always re-check files that are reported to exist.
                var val = (File.Exists(path), buildStartTicks);
                return _existenceCache.AddOrUpdate(
                    path,
                    val, 
                    (_, _) => val).exists;
            }
            return e.exists;
        }
    }
}
