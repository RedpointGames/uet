namespace Redpoint.AutoDiscovery.Windows
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DNS_SERVICE_CANCEL
    {
        public unsafe void* Reserved;
    }
}
