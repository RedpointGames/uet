namespace Redpoint.AutoDiscovery.Windows
{
    extern alias SDWin64;
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using SDWin64::Windows.Win32;
    using SDWin64::Windows.Win32.Foundation;
    using SDWin64::Windows.Win32.NetworkManagement.Dns;

    [SupportedOSPlatform("windows10.0.10240")]
    internal unsafe class Win64ServiceBrowseCall : WindowsNativeRequestCall<DNS_SERVICE_BROWSE_REQUEST, DNS_SERVICE_CANCEL>
    {
        private readonly string _query;

        public Win64ServiceBrowseCall(string query)
        {
            _query = query;
        }

        protected override IEnumerable<nint> ConstructPtrsForRequest()
        {
            yield return Marshal.StringToHGlobalUni(_query);
        }

        protected override DNS_SERVICE_BROWSE_REQUEST ConstructRequest(
            nint id,
            nint[] ptrs,
            IDisposable[] disposables)
        {
            var serviceInstance = (Win64ServiceInstance)disposables[0];
            return new DNS_SERVICE_BROWSE_REQUEST
            {
                Version = (uint)DNS_QUERY_OPTIONS.DNS_QUERY_REQUEST_VERSION2,
                InterfaceIndex = 0,
                QueryName = new PCWSTR((char*)ptrs[0]),
                pQueryContext = (void*)id,
                Anonymous =
                    {
                        pBrowseCallbackV2 = (delegate* unmanaged[Stdcall]<void*, DNS_QUERY_RESULT*, void>)Marshal.GetFunctionPointerForDelegate(RequestReceivedResult),
                    }
            };
        }

        protected override void CancelRequest(DNS_SERVICE_CANCEL* cancel)
        {
            PInvoke.DnsServiceBrowseCancel(cancel);
        }

        protected override unsafe void StartRequest(
            in DNS_SERVICE_BROWSE_REQUEST request,
            WindowsNativeRequest<DNS_SERVICE_BROWSE_REQUEST, DNS_SERVICE_CANCEL> nativeRequest,
            DNS_SERVICE_CANCEL* cancel)
        {
            var result = PInvoke.DnsServiceBrowse(
                request,
                nativeRequest.CancellableRequest.Cancel);
            if (result != PInvoke.DNS_REQUEST_PENDING)
            {
                throw new Win32Exception((int)result);
            }
        }

        private static unsafe void RequestReceivedResult(
            void* queryContext,
            DNS_QUERY_RESULT* result)
        {
            var inflight = _calls[(nint)queryContext];
            if (result == null)
            {
                inflight.ResultException = new InvalidOperationException("Returned DNS_QUERY_RESULT is null.");
                inflight.AsyncSemaphore.Unlock();
                return;
            }

            if (result->QueryStatus != 0)
            {
                inflight.ResultException = new Win32Exception(result->QueryStatus);
            }

            PInvoke.DnsFree(result->pQueryRecords, DNS_FREE_TYPE.DnsFreeRecordList);

            inflight.AsyncSemaphore.Unlock();
        }
    }
}
