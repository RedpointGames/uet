namespace Redpoint.AutoDiscovery.Windows
{
    extern alias SDWin64;
    using System;
    using System.ComponentModel;
    using System.Runtime.Versioning;
    using SDWin64::Windows.Win32;
    using SDWin64::Windows.Win32.Foundation;
    using SDWin64::Windows.Win32.NetworkManagement.Dns;

    [SupportedOSPlatform("windows10.0.10240")]
    internal unsafe class Win64ServiceDeRegisterCall : WindowsNativeRequestCall<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL>
    {
        private readonly Win64ServiceInstance _serviceInstance;

        public Win64ServiceDeRegisterCall(Win64ServiceInstance serviceInstance)
        {
            _serviceInstance = serviceInstance;
        }

        protected override IEnumerable<IDisposable> ConstructDisposablesForRequest()
        {
            yield return _serviceInstance;
        }

        protected override DNS_SERVICE_REGISTER_REQUEST ConstructRequest(
            nint id,
            nint[] ptrs,
            IDisposable[] disposables)
        {
            var serviceInstance = (Win64ServiceInstance)disposables[0];
            return new DNS_SERVICE_REGISTER_REQUEST
            {
                Version = (uint)DNS_QUERY_OPTIONS.DNS_QUERY_REQUEST_VERSION1,
                InterfaceIndex = 0,
                pServiceInstance = serviceInstance.Instance,
                pQueryContext = (void*)id,
                pRegisterCompletionCallback = EndRequest,
                unicastEnabled = false
            };
        }

        protected override void CancelRequest(DNS_SERVICE_CANCEL* cancel)
        {
        }

        protected override unsafe void StartRequest(
            in DNS_SERVICE_REGISTER_REQUEST request,
            WindowsNativeRequest<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL> nativeRequest,
            DNS_SERVICE_CANCEL* cancel)
        {
            var result = PInvoke.DnsServiceDeRegister(request, null);
            if (result != PInvoke.DNS_REQUEST_PENDING)
            {
                throw new Win32Exception((int)result);
            }
        }

        private static unsafe void EndRequest(
            uint status,
            void* queryContext,
            DNS_SERVICE_INSTANCE* instance)
        {
            var inflight = _calls[(nint)queryContext];
            if (status != (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                inflight.ResultException = new Win32Exception((int)status);
            }
            inflight.AsyncSemaphore.Unlock();
        }
    }
}
