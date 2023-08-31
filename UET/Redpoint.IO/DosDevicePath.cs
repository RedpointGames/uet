namespace Redpoint.IO
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.Win32;

    public static class DosDevicePath
    {
        [SupportedOSPlatform("windows5.1.2600")]
        public unsafe static string GetFullyQualifiedDosDevicePath(string path)
        {
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
            if (dosDevice == string.Empty)
            {
                throw new InvalidOperationException($"Unable to resolve DosDevice for path root '{driveRoot}'");
            }
            return (dosDevice + '\\' + path.Substring(3)).TrimEnd('\\');
        }
    }
}
