﻿namespace Redpoint.Logging.Mac
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    internal static partial class MacNative
    {
        static MacNative()
        {
            NativeLibrary.SetDllImportResolver(typeof(MacNative).Assembly, ImportResolver);
        }

        private static nint ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            nint handle = nint.Zero;
            if (libraryName == "Logging")
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    handle = NativeLibrary.Load("libLogging.x64.dylib", assembly, null);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    handle = NativeLibrary.Load("libLogging.arm64.dylib", assembly, null);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }
            return handle;
        }

        [LibraryImport("System", EntryPoint = "os_log_create")]
        public static partial nint os_log_create(
            [MarshalAs(UnmanagedType.LPStr)] string subsystem,
            [MarshalAs(UnmanagedType.LPStr)] string category);

        [LibraryImport("Logging", EntryPoint = "redpoint_os_log")]
        public static partial nint redpoint_os_log(
            nint osLog,
            int type,
            [MarshalAs(UnmanagedType.LPStr)] string message);
    }
}
