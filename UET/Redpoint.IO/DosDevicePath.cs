namespace Redpoint.IO
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Windows.Win32;

    /// <summary>
    /// Provides APIs for interacting with DOS device paths on Windows.
    /// </summary>
    public static class DosDevicePath
    {
        /// <summary>
        /// Returns the fully qualified DOS device path (such as \Device\HarddiskVolume3\Directory\File)
        /// for the specified input path (such as C:\Directory\File). DOS device paths always refer
        /// to the same file on the same hard disk volume, regardless of drive letter mappings, which
        /// makes them suitable for junction targets.
        /// </summary>
        /// <param name="path">The original path, such as C:\Directory\File.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">The <paramref name="path"/> value is null.</exception>
        /// <exception cref="Win32Exception">A native error was returned from the Win32 QueryDosDevice call.</exception>
        /// <exception cref="InvalidOperationException">The QueryDosDevice call did not return a device name for the specified path.</exception>
        [SupportedOSPlatform("windows5.1.2600")]
        public unsafe static string GetFullyQualifiedDosDevicePath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            if (path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                // This is a UNC path.
                return string.Concat(@"\Device\Mup\", path.AsSpan(2));
            }
            else
            {
                // This is a local device.
                string dosDevice = string.Empty;
                var driveRoot = Path.GetPathRoot(path)!.TrimEnd('\\');
                {
                    char[] buffer = new char[PInvoke.MAX_PATH];
                    uint length = PInvoke.QueryDosDevice(driveRoot, buffer);
                    if (length == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    int end;
                    for (end = 0; end < buffer.Length; end++)
                    {
                        if (buffer[end] == '\0')
                        {
                            dosDevice = new string(buffer, 0, end);
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(dosDevice))
                {
                    throw new InvalidOperationException($"Unable to resolve DosDevice for path root '{driveRoot}'");
                }
                return (string.Concat(dosDevice, "\\", path.AsSpan(3))).TrimEnd('\\');
            }
        }
    }
}
