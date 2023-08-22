namespace Redpoint.AutoDiscovery.Windows
{
    extern alias SDWin64;
    using System;
    using System.Runtime.Versioning;
    using SDWin64::Windows.Win32;
    using SDWin64::Windows.Win32.Foundation;
    using SDWin64::Windows.Win32.NetworkManagement.Dns;

    [SupportedOSPlatform("windows10.0.10240")]
    internal unsafe class Win64ServiceInstance : IDisposable
    {
        private readonly unsafe DNS_SERVICE_INSTANCE* _instance;
        private bool _keepOnNextDispose;

        public Win64ServiceInstance(
            string name,
            ushort port)
        {
            _instance = PInvoke.DnsServiceConstructInstance(
                name,
                "",
                null,
                null,
                port,
                0,
                0,
                Array.Empty<PCWSTR>(),
                Array.Empty<PCWSTR>());
        }

        public unsafe DNS_SERVICE_INSTANCE* Instance => _instance;

        public void KeepOnNextDispose()
        {
            _keepOnNextDispose = true;
        }

        public void Dispose()
        {
            if (_keepOnNextDispose)
            {
                _keepOnNextDispose = false;
                return;
            }

            PInvoke.DnsServiceFreeInstance(_instance);
        }
    }
}
