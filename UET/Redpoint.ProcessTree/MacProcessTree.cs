namespace Redpoint.ProcessTree
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal sealed partial class MacProcessTree : IProcessTree
    {
        [LibraryImport("libc"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static unsafe partial int sysctl(nint name, uint namelen, nint oldp, ref ulong oldlenp, nint newp, ulong newlenp);

        public Process? GetParentProcess(int processId)
        {
            // @note: The structure of kinfo_proc is fairly complicated, so
            // rather than replicating all of the nested structures here, we got these
            // offsets with the following code:
            /*
             * #include <sys/sysctl.h>
             * #include <sys/stat.h>
             * #include <sys/types.h>
             * #include <stdio.h>
             *
             * int main() {
             *     struct kinfo_proc info;
             *     size_t length = sizeof(struct kinfo_proc);
             *
             *     printf("CTL_KERN %d\n", CTL_KERN);
             *     printf("KERN_PROC %d\n", KERN_PROC);
             *     printf("KERN_PROC_PID %d\n", KERN_PROC_PID);
             *
             *     printf("kinfo_proc %d\n", length);
             *     printf("kinfo_proc offset %d\n", (void*)(&(info.kp_eproc.e_ppid)) - (void*)(&info));
             *     printf("kinfo_proc size of %d\n", sizeof(info.kp_eproc.e_ppid));
             *
             *     return 0;
             * }
             */
            const int kinfoprocSize = 648;
            const int kinfoprocPpidOffset = 560;
            // const int kinfoprocPpidSize = 4;
            const int ctlKern = 1;
            const int kernProc = 14;
            const int kernProcPid = 1;

            nint kinfoproc = Marshal.AllocHGlobal(kinfoprocSize);
            unsafe
            {
                var name = new int[4] { ctlKern, kernProc, kernProcPid, processId };
                var gcHandle = GCHandle.Alloc(name, GCHandleType.Pinned);
                try
                {
                    ulong kinfoprocLength = kinfoprocSize;
                    int result = sysctl(
                        gcHandle.AddrOfPinnedObject(),
                        4,
                        kinfoproc,
                        ref kinfoprocLength,
                        0,
                        0);
                    if (result < 0)
                    {
                        return null;
                    }
                    if (kinfoprocLength == 0)
                    {
                        return null;
                    }
                    int* ppidAddr = (int*)(kinfoproc + kinfoprocPpidOffset);
                    return Process.GetProcessById(*ppidAddr);
                }
                finally
                {
                    gcHandle.Free();
                }
            }
        }

        public Process? GetParentProcess()
        {
            return GetParentProcess(Environment.ProcessId);
        }

        public Process? GetParentProcess(Process process)
        {
            return GetParentProcess(process.Id);
        }
    }
}