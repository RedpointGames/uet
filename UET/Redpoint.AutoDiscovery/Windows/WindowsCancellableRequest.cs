namespace Redpoint.AutoDiscovery.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    internal class WindowsCancellableRequest<TCancel> : IDisposable where TCancel : unmanaged
    {
        private readonly nint _serviceCancel;
        private readonly CancellationTokenRegistration _cancellationTokenRegistration;
        private bool _requestStarted;
        private readonly CancellationToken _cancellationToken;
        private readonly Action<nint> _uponCancel;

        public unsafe WindowsCancellableRequest(
            CancellationToken cancellationToken,
            Action<nint> uponCancel)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _serviceCancel = Marshal.AllocHGlobal(sizeof(TCancel));
            _cancellationTokenRegistration = cancellationToken.Register(() =>
            {
                if (_requestStarted)
                {
                    uponCancel(_serviceCancel);
                }
            });
            _requestStarted = false;
            _cancellationToken = cancellationToken;
            _uponCancel = uponCancel;
        }

        public unsafe TCancel* Cancel => (TCancel*)_serviceCancel;

        public void Started()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _uponCancel(_serviceCancel);
            }
            else if (!_requestStarted)
            {
                _requestStarted = true;
            }
        }

        public void Dispose()
        {
            _cancellationTokenRegistration.Unregister();
            Marshal.FreeHGlobal(_serviceCancel);
        }
    }
}
