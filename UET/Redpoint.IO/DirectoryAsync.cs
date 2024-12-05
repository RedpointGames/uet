namespace Redpoint.IO
{
    /// <summary>
    /// Provides asynchronous APIs for interacting with directories.
    /// </summary>
    public static class DirectoryAsync
    {
        /// <summary>
        /// Delete the specified directory asynchronously. This method also automatically
        /// removes the read-only flags on files and subdirectories to ensure the
        /// delete operation succeeds.
        /// </summary>
        /// <param name="path">The path to the directory to delete.</param>
        /// <param name="recursive">If true, the directory is deleted recursively.</param>
        /// <returns></returns>
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
                catch (DirectoryNotFoundException)
                {
                    // Directory already doesn't exist; ignore.
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Move the specified directory asynchronously.
        /// </summary>
        /// <param name="source">The current path of the directory.</param>
        /// <param name="target">The new path of the directory.</param>
        /// <returns></returns>
        public static async Task MoveAsync(string source, string target)
        {
            await Task.Run(() =>
            {
                Directory.Move(source, target);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies the specified directory asynchronously. This method does not remove existing
        /// files in the target location, and will only copy files over if they do not currently
        /// exist or if the file in the target is older than the file in the source.
        /// </summary>
        /// <param name="source">The current path of the directory.</param>
        /// <param name="target">The path the directory should be copied to.</param>
        /// <param name="recursive">If true, the directory is copied recursively.</param>
        /// <returns></returns>
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
                        await CopyAsync(sd.FullName, targetPath, true).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}