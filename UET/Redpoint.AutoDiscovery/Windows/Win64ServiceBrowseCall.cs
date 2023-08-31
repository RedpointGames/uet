namespace Redpoint.AutoDiscovery.Windows
{
    extern alias SDWin64;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Redpoint.Concurrency;
    using SDWin64::Windows.Win32;
    using SDWin64::Windows.Win32.Foundation;
    using SDWin64::Windows.Win32.NetworkManagement.Dns;

    [SupportedOSPlatform("windows10.0.10240")]
    internal unsafe class Win64ServiceBrowseCall : WindowsNativeRequestCall<DNS_SERVICE_BROWSE_REQUEST, DNS_SERVICE_CANCEL>
    {
        private readonly string _query;
        private readonly TerminableAwaitableConcurrentQueue<NetworkService> _resultStream;

        public Win64ServiceBrowseCall(
            string query,
            TerminableAwaitableConcurrentQueue<NetworkService> resultStream)
        {
            _query = query;
            _resultStream = resultStream;
        }

        protected override IEnumerable<nint> ConstructPtrsForRequest()
        {
            yield return Marshal.StringToHGlobalAuto(_query);
        }

        protected override object? GetCustomDataForRequest()
        {
            return _resultStream;
        }

        protected override DNS_SERVICE_BROWSE_REQUEST ConstructRequest(
            nint id,
            nint[] ptrs,
            IDisposable[] disposables)
        {
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
            _resultStream.Terminate();
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
            var stream = (TerminableAwaitableConcurrentQueue<NetworkService>)inflight.CustomData!;
            if (result == null)
            {
                try
                {
                    throw new InvalidOperationException("Returned DNS_QUERY_RESULT is null.");
                }
                catch (Exception ex)
                {
                    inflight.ResultException = ExceptionDispatchInfo.Capture(ex);
                }
                PInvoke.DnsServiceBrowseCancel(inflight.CancellableRequest.Cancel);
                stream.Terminate();
                inflight.AsyncSemaphore.Open();
                return;
            }
            else if (result->QueryStatus != 0)
            {
                try
                {
                    throw new Win32Exception(result->QueryStatus);
                }
                catch (Exception ex)
                {
                    inflight.ResultException = ExceptionDispatchInfo.Capture(ex);
                }
                PInvoke.DnsServiceBrowseCancel(inflight.CancellableRequest.Cancel);
                stream.Terminate();
                inflight.AsyncSemaphore.Open();
                return;
            }
            else
            {
                // Stream results.
                var current = result->pQueryRecords;
                while (current != null)
                {
                    var name = Marshal.PtrToStringAuto((nint)current->pName.Value);

                    switch ((DNS_TYPE)current->wType)
                    {
                        case DNS_TYPE.DNS_TYPE_SRV:
                            var nameTarget = Marshal.PtrToStringAuto((nint)current->Data.SRV.pNameTarget.Value);
                            var port = current->Data.SRV.wPort;
                            var priority = current->Data.SRV.wPriority;
                            var weight = current->Data.SRV.wWeight;
                            try
                            {
                                var addressList = System.Net.Dns.GetHostEntry(nameTarget!).AddressList;
                                if (addressList != null && addressList.Length > 0)
                                {
                                    stream.Enqueue(new NetworkService
                                    {
                                        ServiceName = name!,
                                        TargetHostname = nameTarget!,
                                        TargetAddressList = addressList!,
                                        TargetPort = port,
                                    });
                                }
                            }
                            catch (Exception)
                            {
                                // If the hostname can't be resolved, ignore
                                // the DNS-SD entry.
                            }
                            break;
                        case DNS_TYPE.DNS_TYPE_PTR:
                            break;
                        case DNS_TYPE.DNS_TYPE_TEXT:
                            break;
                        default:
                            Debugger.Break();
                            break;
                    }

                    current = current->pNext;
                }
                PInvoke.DnsFree(result->pQueryRecords, DNS_FREE_TYPE.DnsFreeRecordList);
            }
        }
    }
}
