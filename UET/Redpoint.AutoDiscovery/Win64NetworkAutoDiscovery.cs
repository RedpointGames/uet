namespace Redpoint.AutoDiscovery
{
    extern alias SDWin64;

    using Redpoint.AutoDiscovery.Windows;
    using Redpoint.Concurrency;
    using SDWin64::Windows.Win32;
    using SDWin64::Windows.Win32.Foundation;
    using SDWin64::Windows.Win32.NetworkManagement.Dns;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;

    [SupportedOSPlatform("windows10.0.10240")]
    internal class Win64NetworkAutoDiscovery : INetworkAutoDiscovery
    {
        private class DnsDeregisterAsyncDisposable : IAsyncDisposable
        {
            private readonly Win64ServiceInstance _serviceInstance;

            public DnsDeregisterAsyncDisposable(Win64ServiceInstance serviceInstance)
            {
                _serviceInstance = serviceInstance;
            }

            public async ValueTask DisposeAsync()
            {
                var request = new Win64ServiceDeRegisterCall(_serviceInstance);
                await request.ExecuteAsync(CancellationToken.None);
            }
        }

        public async Task<IAsyncDisposable> RegisterServiceAsync(
            string name,
            int port,
            CancellationToken cancellationToken)
        {
            var request = new Win64ServiceRegisterCall(name, (ushort)port);
            var nativeRequest = await request.ExecuteAsync(cancellationToken);
            var serviceInstance = (Win64ServiceInstance)nativeRequest.DisposablePtrs[0];
            return new DnsDeregisterAsyncDisposable(serviceInstance);
        }

        public IAsyncEnumerable<NetworkService> DiscoverServicesAsync(string name)
        {
            throw new InvalidOperationException();
        }


#if FALSE
        private static readonly WindowsNativeRequestCollection<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL> _register;
        private static readonly WindowsNativeRequestCollection<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL> _unregister;
        private static readonly WindowsNativeRequestCollection<DNS_SERVICE_BROWSE_REQUEST, DNS_SERVICE_CANCEL> _browse;

        static Win64NetworkAutoDiscovery()
        {
            _register = new WindowsNativeRequestCollection<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL>();
            _unregister = new WindowsNativeRequestCollection<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL>();
            _browse = new WindowsNativeRequestCollection<DNS_SERVICE_BROWSE_REQUEST, DNS_SERVICE_CANCEL>();
        }

        private static unsafe WindowsNativeRequest<DNS_SERVICE_REGISTER_REQUEST, DNS_SERVICE_CANCEL> StartRegisterService(string name, int port, CancellationToken cancellationToken)
        {
            var requestPending = false;
            var serviceInstance = new Win64ServiceInstance(name, (ushort)port);
            try
            {
                var registerRequestInstance = new DNS_SERVICE_REGISTER_REQUEST
                {
                    Version = (uint)DNS_QUERY_OPTIONS.DNS_QUERY_REQUEST_VERSION1,
                    InterfaceIndex = 0,
                    pServiceInstance = serviceInstance.Instance,
                    pRegisterCompletionCallback = EndRegisterService,
                    unicastEnabled = false
                };
                var request = _register.Add(
                    registerRequestInstance,
                    Array.Empty<nint>(),
                    new IDisposable[] { serviceInstance },
                    cancel =>
                    {
                        PInvoke.DnsServiceRegisterCancel((DNS_SERVICE_CANCEL*)cancel);
                    },
                    cancellationToken);
                registerRequestInstance.pQueryContext = (void*)request.Id;
                try
                {
                    var result = PInvoke.DnsServiceRegister(registerRequestInstance, request.CancellableRequest.Cancel);
                    if (result != PInvoke.DNS_REQUEST_PENDING)
                    {
                        throw new Win32Exception((int)result);
                    }
                    else
                    {
                        requestPending = true;
                        return request;
                    }
                }
                finally
                {
                    if (!requestPending)
                    {
                        _register.Remove(request.Id);
                    }
                }
            }
            finally
            {
                if (!requestPending)
                {
                    serviceInstance.Dispose();
                }
            }
        }

        private static unsafe void EndRegisterService(
            uint status,
            void* queryContext,
            DNS_SERVICE_INSTANCE* instance)
        {
            var inflight = _register[(nint)queryContext];
            if (status != (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                inflight.ResultException = new Win32Exception((int)status);
            }
            inflight.AsyncSemaphore.Unlock();
        }

        private static unsafe InflightDeRegisterRequest StartDeRegisterService(
            DNS_SERVICE_REGISTER_REQUEST registerRequestInstance,
            nint serviceInstanceRaw)
        {
            var serviceInstance = (DNS_SERVICE_INSTANCE*)serviceInstanceRaw;
            if (registerRequestInstance.pServiceInstance != serviceInstance)
            {
                throw new InvalidOperationException();
            }

            var requestPending = false;
            var requestId = _nextId++;
            var request = new InflightDeRegisterRequest
            {
                Id = requestId,
                ServiceInstance = (nint)serviceInstance,
                RegisterRequest = registerRequestInstance,
            };
            _inflightDeRegister[requestId] = request;
            try
            {
                registerRequestInstance.pRegisterCompletionCallback = EndDeRegisterService;
                registerRequestInstance.pQueryContext = (void*)requestId;
                var result = PInvoke.DnsServiceDeRegister(registerRequestInstance, null);
                if (result != PInvoke.DNS_REQUEST_PENDING)
                {
                    throw new Win32Exception((int)result);
                }
                else
                {
                    requestPending = true;
                    return request;
                }
            }
            finally
            {
                if (!requestPending)
                {
                    _inflightDeRegister.TryRemove(requestId, out _);
                    PInvoke.DnsServiceFreeInstance(serviceInstance);
                }
            }
        }

        private static unsafe void EndDeRegisterService(
            uint status,
            void* queryContext,
            DNS_SERVICE_INSTANCE* instance)
        {
            var inflight = _inflightDeRegister[(nint)queryContext];
            if (status != (uint)WIN32_ERROR.ERROR_SUCCESS)
            {
                inflight.ResultException = new Win32Exception((int)status);
            }
            PInvoke.DnsServiceFreeInstance((DNS_SERVICE_INSTANCE*)inflight.ServiceInstance);
            inflight.AsyncSemaphore.Unlock();
        }

        private class DnsDeregisterAsyncDisposable : IAsyncDisposable
        {
            private readonly DNS_SERVICE_REGISTER_REQUEST _request;
            private readonly nint _service;

            public DnsDeregisterAsyncDisposable(
                DNS_SERVICE_REGISTER_REQUEST request,
                nint service)
            {
                _request = request;
                _service = service;
            }

            public async ValueTask DisposeAsync()
            {
                var request = StartDeRegisterService(_request, _service);
                await request.AsyncSemaphore.WaitAsync(CancellationToken.None);
                _inflightRegister.TryRemove(request.Id, out _);
                if (request.ResultException != null)
                {
                    throw request.ResultException;
                }
            }
        }

        public async Task<IAsyncDisposable> RegisterServiceAsync(string name, int port, CancellationToken cancellationToken)
        {
            var request = StartRegisterService(name, port);
            await request.AsyncSemaphore.WaitAsync(cancellationToken);
            _inflightRegister.TryRemove(request.Id, out _);
            if (request.ResultException != null)
            {
                throw request.ResultException;
            }
            return new DnsDeregisterAsyncDisposable(request.RegisterRequest, request.ServiceInstance);
        }

        private static unsafe void EndBrowse(
            void* QueryContext,
            DNS_QUERY_RESULT* QueryResult)
        {

        }

        private static unsafe InflightBrowseRequest StartBrowse(string name)
        {
            var requestPending = false;
            nint queryName = Marshal.StringToHGlobalUni(name);
            try
            {
                var requestId = _nextId++;
                var browseRequestInstance = new DNS_SERVICE_BROWSE_REQUEST
                {
                    Version = (uint)DNS_QUERY_OPTIONS.DNS_QUERY_REQUEST_VERSION2,
                    InterfaceIndex = 0,
                    QueryName = new PCWSTR((char*)queryName),
                    pQueryContext = (void*)requestId,
                    Anonymous =
                    {
                        pBrowseCallbackV2 = (delegate* unmanaged[Stdcall]<void*, DNS_QUERY_RESULT*, void>)Marshal.GetFunctionPointerForDelegate(EndBrowse),
                    }
                };
                var request = new InflightBrowseRequest
                {
                    Id = requestId,
                    QueryName = queryName,
                    BrowseRequest = browseRequestInstance,
                };
                _inflightBrowse[requestId] = request;
                try
                {
                    var result = PInvoke.DnsServiceBrowse(
                        browseRequestInstance,
                        null);
                    if (result != PInvoke.DNS_REQUEST_PENDING)
                    {
                        throw new Win32Exception((int)result);
                    }
                    else
                    {
                        requestPending = true;
                        return request;
                    }
                }
                finally
                {
                    if (!requestPending)
                    {
                        _inflightBrowse.TryRemove(requestId, out _);
                    }
                }
            }
            finally
            {
                if (!requestPending)
                {
                    Marshal.FreeHGlobal(queryName);
                }
            }
        }

        public IAsyncEnumerable<NetworkService> DiscoverServicesAsync(string name)
        {



            throw new NotImplementedException();
        }
#endif
    }
}