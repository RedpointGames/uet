namespace Redpoint.AutoDiscovery.Windows
{
    using System;

    internal abstract class WindowsNativeRequestCall<TRequest, TCancel> where TCancel : unmanaged
    {
        protected static readonly WindowsNativeRequestCollection<TRequest, TCancel> _calls;

        static WindowsNativeRequestCall()
        {
            _calls = new WindowsNativeRequestCollection<TRequest, TCancel>();
        }

        public async Task<WindowsNativeRequest<TRequest, TCancel>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var request = ExecuteStart(cancellationToken);
            try
            {
                await request.AsyncSemaphore.WaitAsync(cancellationToken);
                if (request.ResultException != null)
                {
                    request.ResultException.Throw();
                    throw new InvalidOperationException("Expected ExceptionDispatchInfo.Throw to work.");
                }
                NotifyDisposablesOfSuccessfulRequest(request.DisposablePtrs);
                return request;
            }
            finally
            {
                request.Dispose();
            }
        }

        private unsafe WindowsNativeRequest<TRequest, TCancel> ExecuteStart(CancellationToken cancellationToken)
        {
            var nativeMemoryCaptured = false;
            var ptrs = new List<nint>();
            var disposables = new List<IDisposable>();
            try
            {
                foreach (var ptr in ConstructPtrsForRequest())
                {
                    ptrs.Add(ptr);
                }
                foreach (var disposable in ConstructDisposablesForRequest())
                {
                    disposables.Add(disposable);
                }

                var requestStarted = false;
                var nativeRequest = _calls.Add(
                    id => ConstructRequest(id, ptrs.ToArray(), disposables.ToArray()),
                    ptrs.ToArray(),
                    disposables.ToArray(),
                    cancel =>
                    {
                        CancelRequest((TCancel*)cancel);
                    },
                    cancellationToken);
                nativeRequest.CustomData = GetCustomDataForRequest();
                nativeMemoryCaptured = true;
                try
                {
                    StartRequest(
                        nativeRequest.Request,
                        nativeRequest,
                        nativeRequest.CancellableRequest.Cancel);
                    nativeRequest.CancellableRequest.Started();
                    requestStarted = true;
                    return nativeRequest;
                }
                finally
                {
                    if (!requestStarted)
                    {
                        nativeRequest.Dispose();
                    }
                }
            }
            finally
            {
                if (!nativeMemoryCaptured)
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        protected virtual IEnumerable<nint> ConstructPtrsForRequest()
        {
            return Array.Empty<nint>();
        }

        protected virtual IEnumerable<IDisposable> ConstructDisposablesForRequest()
        {
            return Array.Empty<IDisposable>();
        }

        protected virtual void NotifyDisposablesOfSuccessfulRequest(IDisposable[] disposables)
        {
        }

        protected virtual object? GetCustomDataForRequest()
        {
            return null;
        }

        protected abstract TRequest ConstructRequest(nint id, nint[] ptrs, IDisposable[] disposables);

        protected unsafe abstract void StartRequest(
            in TRequest request,
            WindowsNativeRequest<TRequest, TCancel> nativeRequest,
            TCancel* cancel);

        protected unsafe abstract void CancelRequest(TCancel* cancel);
    }
}
