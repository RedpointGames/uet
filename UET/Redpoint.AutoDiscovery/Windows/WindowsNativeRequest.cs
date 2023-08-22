namespace Redpoint.AutoDiscovery.Windows
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    internal unsafe class WindowsNativeRequest<TRequest, TCancel> : IDisposable where TCancel : unmanaged
    {
        public readonly nint Id;
        public readonly Gate AsyncSemaphore;
        public readonly WindowsCancellableRequest<TCancel> CancellableRequest;
        public readonly TRequest Request;
        public readonly nint[] HGlobalPtrs;
        public readonly IDisposable[] DisposablePtrs;
        public Exception? ResultException;

        public WindowsNativeRequest(
            nint id,
            TRequest request,
            nint[] hGlobalPtrs,
            IDisposable[] disposablePtrs,
            Action<nint> cancelHandler,
            CancellationToken cancellationToken)
        {
            Id = id;
            AsyncSemaphore = new Gate();
            Request = request;
            HGlobalPtrs = hGlobalPtrs;
            DisposablePtrs = disposablePtrs;
            CancellableRequest = new WindowsCancellableRequest<TCancel>(
                cancellationToken,
                cancel =>
                {
                    cancelHandler(cancel);
                });
        }

        public void Dispose()
        {
            foreach (var ptr in DisposablePtrs)
            {
                ptr.Dispose();
            }
            foreach (var ptr in HGlobalPtrs)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
