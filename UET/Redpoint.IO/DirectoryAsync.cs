namespace Redpoint.IO
{
    public static class DirectoryAsync
    {
        public static async Task DeleteAsync(string path, bool recursive = false)
        {
            await Task.Run(() =>
            {
                try
                {
                    Directory.Delete(path, recursive);
                }
                catch (UnauthorizedAccessException)
                {
                    // Try and remove "Read Only" flags on files and directories.
                    foreach (var entry in Directory.GetFileSystemEntries(
                        path,
                        "*",
                        new EnumerationOptions
                        {
                            AttributesToSkip = FileAttributes.System,
                            RecurseSubdirectories = true,
                        }))
                    {
                        var attrs = File.GetAttributes(entry);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            attrs ^= FileAttributes.ReadOnly;
                            File.SetAttributes(entry, attrs);
                        }
                    }

                    // Now try to delete again.
                    Directory.Delete(path, recursive);
                }
            });
        }

        public static async Task MoveAsync(string source, string target)
        {
            await Task.Run(() =>
            {
                Directory.Move(source, target);
            });
        }

        public static async Task CopyAsync(string source, string target, bool recursive)
        {
            await Task.Run(async () =>
            {
                var dir = new DirectoryInfo(source);
                var dirs = dir.GetDirectories();
                Directory.CreateDirectory(target);
                foreach (var f in dir.GetFiles())
                {
                    var targetPath = Path.Combine(target, f.Name);
                    var targetInfo = new FileInfo(targetPath);
                    if (!targetInfo.Exists || f.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc)
                    {
                        f.CopyTo(targetPath, true);
                    }
                }
                if (recursive)
                {
                    foreach (var sd in dirs)
                    {
                        var targetPath = Path.Combine(target, sd.Name);
                        await CopyAsync(sd.FullName, targetPath, true);
                    }
                }
            });
        }
    }
}