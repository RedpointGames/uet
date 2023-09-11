namespace Redpoint.AutoDiscovery.Windows
{
    extern alias SDWin32;
    using System;
    using System.Runtime.Versioning;
    using SDWin32::Windows.Win32;
    using SDWin32::Windows.Win32.Foundation;
    using SDWin32::Windows.Win32.NetworkManagement.Dns;

    [SupportedOSPlatform("windows10.0.10240")]
    internal unsafe sealed class Win32ServiceInstance : IDisposable
    {
        private readonly unsafe DNS_SERVICE_INSTANCE* _instance;
        private bool _keepOnNextDispose;

        public Win32ServiceInstance(
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
