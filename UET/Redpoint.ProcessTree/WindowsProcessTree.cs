namespace Redpoint.ProcessTree
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal sealed partial class WindowsProcessTree : IProcessTree
    {
        // These members must match PROCESS_BASIC_INFORMATION
        private struct ProcessBasicInformation
        {
            internal IntPtr _reserved1;
            internal IntPtr _pebBaseAddress;
            internal IntPtr _reserved2_0;
            internal IntPtr _reserved2_1;
            internal IntPtr _uniqueProcessId;
            internal IntPtr _inheritedFromUniqueProcessId;
        }

        [LibraryImport("ntdll.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

        private static Process? GetParentProcess(IntPtr handle)
        {
            ProcessBasicInformation pbi = new ProcessBasicInformation();
            int returnLength;
            int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
            if (status != 0)
                throw new Win32Exception(status);

            try
            {
                return Process.GetProcessById(pbi._inheritedFromUniqueProcessId.ToInt32());
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public Process? GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess());
        }

        public Process? GetParentProcess(int id)
        {
            var process = Process.GetProcessById(id);
            if (process == null)
            {
                return null;
            }
            return GetParentProcess(process.Handle);
        }

        public Process? GetParentProcess(Process process)
        {
            return GetParentProcess(process.Handle);
        }
    }
}