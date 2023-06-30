// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        [LibraryImport("kernel32.dll")]
#if !NO_SUPPRESS_GC_TRANSITION
        [SuppressGCTransition]
#endif
        internal static partial IntPtr GetStdHandle(int nStdHandle);  // param is NOT a handle, but it returns one!

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetConsoleMode(IntPtr handle, out int mode);

        internal static bool IsGetConsoleModeCallSuccessful(IntPtr handle)
        {
            return GetConsoleMode(handle, out _);
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetConsoleMode(IntPtr handle, int mode);

        internal const int ENABLE_PROCESSED_INPUT = 0x0001;
        internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        internal const int STD_OUTPUT_HANDLE = -11;
    }
}
