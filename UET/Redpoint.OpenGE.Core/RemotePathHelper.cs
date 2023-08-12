namespace Redpoint.OpenGE.Core
{
    using System;

    public static class RemotePathHelper
    {
        public static string? RemotifyPath(this string path, bool prefixWithVirtualRoot = true)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("Path must be an absolute path", nameof(path));
            }

            var pathSeperator = Path.DirectorySeparatorChar;
            var pathAltSeparator = Path.AltDirectorySeparatorChar;
            var prefix = prefixWithVirtualRoot ? "{__OPENGE_VIRTUAL_ROOT__}" : string.Empty;

            if (OperatingSystem.IsWindows())
            {
                var root = Path.GetPathRoot(path);
                if (root == null || root.StartsWith('\\'))
                {
                    // Can't be remotified.
                    return null;
                }
                return $"{prefix}{pathSeperator}{root[0]}{pathSeperator}{path.Substring(root.Length).Replace(pathAltSeparator, pathSeperator).TrimEnd(pathSeperator)}";
            }
            else
            {
                return $"{prefix}{pathSeperator}{path.Replace(pathAltSeparator, pathSeperator).TrimEnd(pathSeperator)}";
            }
        }
    }
}
