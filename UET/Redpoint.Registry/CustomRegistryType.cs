namespace Redpoint.Registry
{
    using Microsoft.Win32;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.Win32;
    using Windows.Win32.System.Registry;
    using Windows.Win32.Foundation;
    using System.ComponentModel;
    using System.Runtime.Versioning;
    using System.Runtime.InteropServices;

    [SupportedOSPlatform("windows5.0")]
    public static class CustomRegistryType
    {
        public static unsafe (uint type, byte[] data) GetValue(RegistryKey key, string name)
        {
            ArgumentNullException.ThrowIfNull(key);

            REG_VALUE_TYPE type = REG_VALUE_TYPE.REG_NONE;
            uint dataSize = 0;
            var error = PInvoke.RegQueryValueEx(
                key.Handle,
                name,
                &type,
                null,
                &dataSize);
            if (error != WIN32_ERROR.ERROR_SUCCESS)
            {
                throw new Win32Exception((int)error);
            }

            var buffer = Marshal.AllocHGlobal((int)dataSize);
            try
            {
                error = PInvoke.RegQueryValueEx(
                    key.Handle,
                    name,
                    &type,
                    (byte*)buffer,
                    &dataSize);
                if (error != WIN32_ERROR.ERROR_SUCCESS)
                {
                    throw new Win32Exception((int)error);
                }

                var data = new byte[dataSize];
                Marshal.Copy(buffer, data, 0, (int)dataSize);
                return ((uint)type, data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static unsafe void SetValue(RegistryKey key, string name, uint type, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(key);

            var error = PInvoke.RegSetValueEx(key.Handle, name, (REG_VALUE_TYPE)type, data);
            if (error != WIN32_ERROR.ERROR_SUCCESS)
            {
                throw new Win32Exception((int)error);
            }
        }
    }
}
