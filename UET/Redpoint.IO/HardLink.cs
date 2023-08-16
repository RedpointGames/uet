namespace Redpoint.IO
{
    using System.ComponentModel;
    using System.Runtime.Versioning;
    using Windows.Win32;

    [SupportedOSPlatform("windows5.1.2600")]
    public static class HardLink
    {
        public static void CreateHardLink(
            string pathOfLink,
            string existingFile,
            bool overwrite)
        {
            if (File.Exists(pathOfLink))
            {
                if (!overwrite)
                {
                    throw new IOException("Target already exists for hardlink.");
                }
                else
                {
                    File.Delete(pathOfLink);
                }
            }

            if (!Path.IsPathFullyQualified(pathOfLink))
            {
                pathOfLink = Path.GetFullPath(pathOfLink);
            }
            if (!Path.IsPathFullyQualified(existingFile))
            {
                existingFile = Path.GetFullPath(existingFile);
            }

            if (!pathOfLink.StartsWith(@"\\?\"))
            {
                pathOfLink = $@"\\?\{Path.GetFullPath(pathOfLink)}";
            }
            if (!existingFile.StartsWith(@"\\?\"))
            {
                existingFile = $@"\\?\{Path.GetFullPath(existingFile)}";
            }

            if (!PInvoke.CreateHardLink(
                pathOfLink,
                existingFile))
            {
                throw new IOException($"Unable to create hard link '{pathOfLink}' -> '{existingFile}'.", new Win32Exception());
            }
        }
    }
}
