namespace Redpoint.AutoDiscovery
{
    using Redpoint.AutoDiscovery.Windows;
    using Redpoint.Concurrency;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows10.0.10240")]
    internal class Win64NetworkAutoDiscovery : INetworkAutoDiscovery
    {
        private static readonly ConcurrentDictionary<nint, InflightRegisterRequest> _inflightRegister = new ConcurrentDictionary<nint, InflightRegisterRequest>();
        private static readonly ConcurrentDictionary<nint, InflightDeRegisterRequest> _inflightDeRegister = new ConcurrentDictionary<nint, InflightDeRegisterRequest>();
        private static nint _nextId = 1000;

        private class InflightRegisterRequest
        {
            public required nint Id;
            public readonly Gate AsyncSemaphore = new Gate();
            public nint ServiceInstance;
            public nint RegisterRequest;
            public Exception? ResultException;
        }

        private class InflightDeRegisterRequest
        {
            public required nint Id;
            public readonly Gate AsyncSemaphore = new Gate();
            public nint ServiceInstance;
            public nint RegisterRequest;
            public Exception? ResultException;
        }

        private static unsafe InflightRegisterRequest StartRegisterService(string name, int port)
        {
            var instanceName = Marshal.StringToHGlobalUni(name);

            var requestPending = false;
            var serviceInstance = (DNS_SERVICE_INSTANCE*)Marshal.AllocHGlobal(sizeof(DNS_SERVICE_INSTANCE));
            serviceInstance->InstanceName = (char*)instanceName;
            serviceInstance->Port = (ushort)port;
            try
            {
                var requestId = _nextId++;
                var registerRequestInstance = (DNS_SERVICE_REGISTER_REQUEST*)Marshal.AllocHGlobal(sizeof(DNS_SERVICE_REGISTER_REQUEST));
                registerRequestInstance->Version = 1;
                registerRequestInstance->InterfaceIndex = 0;
                registerRequestInstance->ServiceInstance = serviceInstance;
                registerRequestInstance->CompletionCallback = Marshal.GetFunctionPointerForDelegate(EndRegisterService);
                registerRequestInstance->QueryContext = (void*)requestId;
                registerRequestInstance->UnicastEnabled = false;
                try
                {
                    var request = new InflightRegisterRequest
                    {
                        Id = requestId,
                        ServiceInstance = (nint)serviceInstance,
                        RegisterRequest = (nint)registerRequestInstance,
                    };
                    _inflightRegister[requestId] = request;
                    try
                    {
                        var result = PInvoke.DnsServiceRegister(registerRequestInstance, null);
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
                            _inflightRegister.TryRemove(requestId, out _);
                        }
                    }
                }
                finally
                {
                    if (!requestPending)
                    {
                        Marshal.FreeHGlobal((nint)registerRequestInstance);
                    }
                }
            }
            finally
            {
                if (!requestPending)
                {
                    Marshal.FreeHGlobal((nint)serviceInstance->InstanceName);
                    Marshal.FreeHGlobal((nint)serviceInstance);
                }
            }
        }

        private static unsafe void EndRegisterService(
            uint status,
            void* queryContext,
            DNS_SERVICE_INSTANCE* instance)
        {
            var inflight = _inflightRegister[(nint)queryContext];
            if (status != 0)
            {
                Marshal.FreeHGlobal((nint)inflight.RegisterRequest);
                Marshal.FreeHGlobal((nint)((DNS_SERVICE_INSTANCE*)inflight.ServiceInstance)->InstanceName);
                Marshal.FreeHGlobal(inflight.ServiceInstance);
                inflight.ResultException = new Win32Exception((int)status);
            }
            inflight.AsyncSemaphore.Unlock();
        }

        private static unsafe InflightDeRegisterRequest StartDeRegisterService(
            nint registerRequestInstanceRaw,
            nint serviceInstanceRaw)
        {
            var registerRequestInstance = (DNS_SERVICE_REGISTER_REQUEST*)registerRequestInstanceRaw;
            var serviceInstance = (DNS_SERVICE_INSTANCE*)serviceInstanceRaw;
            if (registerRequestInstance->ServiceInstance != serviceInstance)
            {
                throw new InvalidOperationException();
            }

            var requestPending = false;
            var requestId = _nextId++;
            var request = new InflightDeRegisterRequest
            {
                Id = requestId,
                ServiceInstance = (nint)serviceInstance,
                RegisterRequest = (nint)registerRequestInstance,
            };
            _inflightDeRegister[requestId] = request;
            try
            {
                registerRequestInstance->CompletionCallback = Marshal.GetFunctionPointerForDelegate(EndDeRegisterService);
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
                    Marshal.FreeHGlobal(registerRequestInstanceRaw);
                    Marshal.FreeHGlobal((nint)serviceInstance->InstanceName);
                    Marshal.FreeHGlobal(serviceInstanceRaw);
                }
            }
        }

        private static unsafe void EndDeRegisterService(
            uint status,
            void* queryContext,
            DNS_SERVICE_INSTANCE* instance)
        {
            var inflight = _inflightDeRegister[(nint)queryContext];
            if (status != 0)
            {
                inflight.ResultException = new Win32Exception((int)status);
            }
            Marshal.FreeHGlobal((nint)inflight.RegisterRequest);
            Marshal.FreeHGlobal((nint)((DNS_SERVICE_INSTANCE*)inflight.ServiceInstance)->InstanceName);
            Marshal.FreeHGlobal(inflight.ServiceInstance);
            inflight.AsyncSemaphore.Unlock();
        }

        private class DnsDeregisterAsyncDisposable : IAsyncDisposable
        {
            private readonly nint _request;
            private readonly nint _service;

            public DnsDeregisterAsyncDisposable(
                nint request,
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

        public IAsyncEnumerable<NetworkService> DiscoverServicesAsync(string name)
        {
            throw new NotImplementedException();
        }
    }
}