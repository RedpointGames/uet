namespace Redpoint.Uet.Automation.SystemResources
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsSystemResources : ISystemResources
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public bool CanQuerySystemResources => true;

        public ValueTask<(ulong availableMemoryBytes, ulong totalMemoryBytes)> GetMemoryInfo()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return ValueTask.FromResult((memStatus.ullAvailPhys, memStatus.ullTotalPhys));
            }
            return ValueTask.FromResult((0uL, 1uL));
        }
    }
}
