namespace Redpoint.IO
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Windows.Win32;
    using Windows.Win32.Foundation;

    [SupportedOSPlatform("windows5.1.2600")]
    public static class HardLink
    {
        public static void CreateHardLink(
            string pathOfLink,
            string existingFile,
            bool overwrite)
        {
            if (pathOfLink == null) throw new ArgumentNullException(nameof(pathOfLink));
            if (existingFile == null) throw new ArgumentNullException(nameof(existingFile));

            if (!Path.IsPathFullyQualified(pathOfLink))
            {
                pathOfLink = Path.GetFullPath(pathOfLink);
            }
            if (!Path.IsPathFullyQualified(existingFile))
            {
                existingFile = Path.GetFullPath(existingFile);
            }

            if (!pathOfLink.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                pathOfLink = $@"\\?\{Path.GetFullPath(pathOfLink)}";
            }
            if (!existingFile.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                existingFile = $@"\\?\{Path.GetFullPath(existingFile)}";
            }

            if (!PInvoke.CreateHardLink(
                pathOfLink,
                existingFile))
            {
                var errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();
                if (errorCode == WIN32_ERROR.ERROR_FILE_EXISTS ||
                    errorCode == WIN32_ERROR.ERROR_ALREADY_EXISTS)
                {
                    if (overwrite)
                    {
                        // Retry after deletion.
                        File.Delete(pathOfLink);
                        if (!PInvoke.CreateHardLink(
                            pathOfLink,
                            existingFile))
                        {
                            throw new IOException($"Unable to create hard link '{pathOfLink}' -> '{existingFile}'.", new Win32Exception());
                        }
                        return;
                    }
                }
                throw new IOException($"Unable to create hard link '{pathOfLink}' -> '{existingFile}'.", new Win32Exception((int)errorCode));
            }
        }
    }
}
