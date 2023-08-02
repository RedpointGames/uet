namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using System;

    internal static class RemotePathHelper
    {
        public static string? RemotifyPath(this string path)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("Path must be an absolute path", nameof(path));
            }

            if (OperatingSystem.IsWindows())
            {
                var root = Path.GetPathRoot(path);
                if (root == null || root.StartsWith('\\'))
                {
                    // Can't be remotified.
                    return null;
                }
                return $"{{__OPENGE_VIRTUAL_ROOT__}}/{root[0]}/{path.Substring(root.Length).Replace('\\', '/')}";
            }
            else
            {
                return $"{{__OPENGE_VIRTUAL_ROOT__}}/{path.TrimStart('/')}";
            }
        }
    }
}
