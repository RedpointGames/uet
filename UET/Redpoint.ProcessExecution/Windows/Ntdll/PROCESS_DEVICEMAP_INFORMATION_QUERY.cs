namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PROCESS_DEVICEMAP_INFORMATION_QUERY
    {
        public uint DriveMap;
        public fixed byte DriveType[32];
    }
}
