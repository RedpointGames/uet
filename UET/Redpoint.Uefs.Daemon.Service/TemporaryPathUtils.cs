namespace Redpoint.Uefs.Daemon.Service
{
    internal static class PathUtils
    {
        private static List<string> _allocatedPaths = new List<string>();

        public static string GetTemporaryWriteLayerPath(string targetPath)
        {
            if (OperatingSystem.IsWindows())
            {
                var driveRoot = Path.GetPathRoot(targetPath);
                var tempPath = Path.Combine(driveRoot!, "TEMP", "uefs-write-layers");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }
                string allocatedPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
                while (Directory.Exists(allocatedPath))
                {
                    allocatedPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
                }
                Directory.CreateDirectory(allocatedPath);
                _allocatedPaths.Add(allocatedPath);
                return allocatedPath;
            }
            else
            {
                string allocatedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                while (Directory.Exists(allocatedPath))
                {
                    allocatedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                }
                Directory.CreateDirectory(allocatedPath);
                _allocatedPaths.Add(allocatedPath);
                return allocatedPath;
            }
        }

        public static void CleanupDriveLocalPaths()
        {
            foreach (var allocatedPath in _allocatedPaths)
            {
                if (Directory.Exists(allocatedPath))
                {
                    Directory.Delete(allocatedPath, true);
                }
            }
        }

        public static bool IsPathEqual(string pathA, string pathB)
        {
            return pathA?.ToLowerInvariant() == pathB?.ToLowerInvariant();
        }
    }
}
