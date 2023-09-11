namespace Redpoint.AutoDiscovery.Windows
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    internal unsafe sealed class WindowsNativeRequest<TRequest, TCancel> : IDisposable where TCancel : unmanaged
    {
        public readonly nint Id;
        public readonly Gate AsyncSemaphore;
        public readonly WindowsCancellableRequest<TCancel> CancellableRequest;
        public readonly TRequest Request;
        public readonly nint[] HGlobalPtrs;
        public readonly IDisposable[] DisposablePtrs;
        public ExceptionDispatchInfo? ResultException;
        public object? CustomData;

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
                cancel =>
                {
                    cancelHandler(cancel);
                    try
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        ResultException = ExceptionDispatchInfo.Capture(ex);
                    }
                    AsyncSemaphore.Open();
                },
                cancellationToken);
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
            CancellableRequest.Dispose();
        }
    }
}
