namespace Redpoint.ProcessExecution.Windows.SystemCopy
{
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.InteropServices;
    using global::Windows.Win32;
    using global::Windows.Win32.Foundation;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows5.1.2600")]
    internal sealed class ProcessWaitHandle : WaitHandle
    {
        internal unsafe ProcessWaitHandle(HANDLE processHandle)
        {
            var currentProcHandle = PInvoke.GetCurrentProcess();
            HANDLE waitHandle;
            bool succeeded = PInvoke.DuplicateHandle(
                currentProcHandle,
                processHandle,
                currentProcHandle,
                &waitHandle,
                0,
                false,
                DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS);

            if (!succeeded)
            {
                int error = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(error);
            }

            this.SetSafeWaitHandle(new SafeWaitHandle(waitHandle, true));
        }
    }
}
