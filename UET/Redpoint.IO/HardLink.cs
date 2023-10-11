namespace Redpoint.IO
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Windows.Win32;
    using Windows.Win32.Foundation;

    /// <summary>
    /// Provides APIs for interacting with hard links on Windows.
    /// </summary>
    [SupportedOSPlatform("windows5.1.2600")]
    public static class HardLink
    {
        /// <summary>
        /// Creates a file hard link on Windows.
        /// </summary>
        /// <param name="pathOfLink">The path that the hard link should be created at.</param>
        /// <param name="existingFile">The target file that the hard link should point at.</param>
        /// <param name="overwrite">If true and there is an existing file at <paramref name="pathOfLink"/>, it will be removed.</param>
        /// <exception cref="ArgumentNullException">One or more arguments have null values.</exception>
        /// <exception cref="IOException">The hard link could not be created due to a native Win32 error.</exception>
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
