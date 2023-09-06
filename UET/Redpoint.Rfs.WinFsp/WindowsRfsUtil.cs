namespace Redpoint.Rfs.WinFsp
{
    using System;

    internal static class WindowsRfsUtil
    {
        public static bool IsRealPath(string fullPath)
        {
            return !(fullPath.Length < 2 ||
                fullPath[0] != '\\' ||
                (fullPath.Length > 2 && fullPath[2] != '\\'));
        }

        public static string RealPath(string fullPath)
        {
            if (!IsRealPath(fullPath))
            {
                throw new InvalidOperationException("RealPath can only be used with paths underneath a drive letter.");
            }
            return @$"{fullPath[1]}:{fullPath.Substring(2)}";
        }
    }
}
