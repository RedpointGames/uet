namespace Redpoint.ProcessExecution.Windows
{
    using System;
    using System.ComponentModel;
    using System.Text;
    using global::Windows.Win32;
    using global::Windows.Win32.Security;
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.System.Memory;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows5.1.2600")]
    internal class WindowsWorkingDirectory
    {
        /// <remarks>
        /// CsWin32 doesn't generate quite what we need here - we need lpStartAddress to be just
        /// a generic pointer we can pass things into; not a C# delegate type.
        /// </remarks>
        [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows5.1.2600")]
        private static extern unsafe HANDLE CreateRemoteThread(
            HANDLE hProcess,
            [Optional] SECURITY_ATTRIBUTES* lpThreadAttributes,
            nuint dwStackSize,
            void* lpStartAddress,
            [Optional] void* lpParameter,
            uint dwCreationFlags,
            [Optional] uint* lpThreadId);

        internal static unsafe void SetWorkingDirectoryOfAnotherProcess(HANDLE handle, string newWorkingDirectory)
        {
            // Allocate memory in the remote process for our working directory string.
            void* remoteMemory = PInvoke.VirtualAllocEx(
                handle,
                null,
                (nuint)(newWorkingDirectory.Length * 2 + 1),
                VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT,
                PAGE_PROTECTION_FLAGS.PAGE_READWRITE);
            if (remoteMemory == null)
            {
                throw new Win32Exception();
            }
            try
            {
                // Copy the working directory string to the remote process.
                var buffer = new byte[newWorkingDirectory.Length * 2 + 1];
                var workingDirectoryBytes = Encoding.Unicode.GetBytes(newWorkingDirectory);
                Array.Copy(
                    workingDirectoryBytes,
                    buffer,
                    workingDirectoryBytes.Length);
                buffer[buffer.Length - 1] = 0;
                fixed (byte* b = buffer)
                {
                    if (!PInvoke.WriteProcessMemory(
                        handle,
                        remoteMemory,
                        b,
                        (nuint)buffer.Length,
                        null))
                    {
                        throw new Win32Exception();
                    }
                }

                // Get the module handle for kernel32.dll, and the procedure address
                // for SetCurrentDirectoryW.
                using (var kernel32 = PInvoke.GetModuleHandle("kernel32"))
                {
                    var address = PInvoke.GetProcAddress(kernel32, "SetCurrentDirectoryW");
                    if (address.Value == 0)
                    {
                        throw new NullReferenceException("Unable to locate 'SetCurrentDirectoryW' in Kernel32.");
                    }

                    // Create a remote thread in the target process; our thread in this case
                    // is actually just the "SetCurrentDirectoryW" call directly, since it
                    // matches the function signature of the ThreadProc delegate. If it didn't
                    // match, we'd have to inject our own wrapper function into the process...
                    var remoteThread = CreateRemoteThread(
                        handle,
                        null,
                        0,
                        (void*)address.Value,
                        remoteMemory,
                        0,
                        null);
                    PInvoke.WaitForSingleObject(remoteThread, PInvoke.INFINITE);
                    uint exitCode = 128;
                    PInvoke.GetExitCodeThread(remoteThread, &exitCode);
                    PInvoke.CloseHandle(remoteThread);
                }
            }
            finally
            {
                PInvoke.VirtualFreeEx(handle, remoteMemory, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
            }
        }
    }
}
