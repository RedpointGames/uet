namespace Redpoint.AutoDiscovery.Windows
{
    using System;
    using System.Collections.Concurrent;

    internal unsafe sealed class WindowsNativeRequestCollection<TRequest, TCancel> where TCancel : unmanaged
    {
        private readonly ConcurrentDictionary<nint, WindowsNativeRequest<TRequest, TCancel>> _requests;
        private nint _nextId;

        public WindowsNativeRequestCollection()
        {
            _requests = new ConcurrentDictionary<nint, WindowsNativeRequest<TRequest, TCancel>>();
        }

        public WindowsNativeRequest<TRequest, TCancel> Add(
            Func<nint, TRequest> requestFactory,
            nint[] hGlobalPtrs,
            IDisposable[] disposablePtrs,
            Action<nint> cancelHandler,
            CancellationToken cancellationToken)
        {
            var id = _nextId++;
            var nativeRequest = new WindowsNativeRequest<TRequest, TCancel>(
                id,
                requestFactory(id),
                hGlobalPtrs,
                disposablePtrs,
                cancelHandler,
                cancellationToken);
            _requests[id] = nativeRequest;
            return nativeRequest;
        }

        public WindowsNativeRequest<TRequest, TCancel> this[nint id]
        {
            get
            {
                return _requests[id];
            }
        }

        public void Remove(nint id)
        {
            if (_requests.TryRemove(id, out var entry))
            {
                entry.Dispose();
            }
        }
    }
}
