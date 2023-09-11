using System;

namespace Redpoint.IO
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Windows.Win32;

    public static class DosDevicePath
    {
        [SupportedOSPlatform("windows5.1.2600")]
        public unsafe static string GetFullyQualifiedDosDevicePath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

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
                    fixed (char* bufferPtr = buffer)
                    {
                        uint length = PInvoke.QueryDosDevice(driveRoot, bufferPtr, (uint)buffer.Length);
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
