namespace Redpoint.UET.Core
{
    public static class DirectoryAsync
    {
        public static async Task DeleteAsync(string path, bool recursive = false)
        {
            await Task.Run(() =>
            {
                Directory.Delete(path, recursive);
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