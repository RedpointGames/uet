namespace Redpoint.AutoDiscovery.Windows
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DNS_SERVICE_REGISTER_REQUEST
    {
        public uint Version;
        public uint InterfaceIndex;
        public unsafe DNS_SERVICE_INSTANCE* ServiceInstance;
        public nint CompletionCallback;
        public unsafe void* QueryContext;
        public nint Credentials;
        public bool UnicastEnabled;
    }
}
