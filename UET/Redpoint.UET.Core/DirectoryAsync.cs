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
    }
}