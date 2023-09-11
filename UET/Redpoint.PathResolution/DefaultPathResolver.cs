namespace Redpoint.PathResolution
{
    internal class DefaultPathResolver : IPathResolver
    {
        public Task<string> ResolveBinaryPath(string binaryName)
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var pathExts = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();

            foreach (var path in paths)
            {
                if (pathExts.Length > 0)
                {
                    foreach (var pathExt in pathExts)
                    {
                        if (File.Exists(Path.Combine(path, $"{binaryName}{pathExt}")))
                        {
                            return Task.FromResult(Path.Combine(path, $"{binaryName}{pathExt}"));
                        }
                    }
                }
                else
                {
                    if (File.Exists(Path.Combine(path, binaryName)))
                    {
                        return Task.FromResult(Path.Combine(path, binaryName));
                    }
                }
            }

            throw new FileNotFoundException($"The '{binaryName}' binary could not be found in any PATH.");
        }
    }
}