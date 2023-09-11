namespace Redpoint.Windows.HandleManagement
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Collections.Concurrent;
    using global::Windows.Win32.System.WindowsProgramming;
    using global::Windows.Win32.Foundation;
    using System.Runtime;
    using System.Diagnostics;

    internal sealed class NtQueryObjectPool
    {
        private sealed class ThreadRequest : IDisposable
        {
            public HANDLE Handle;
            public OBJECT_INFORMATION_CLASS ObjectInformationClass;
            public nint ObjectInformation;
            public uint ObjectInformationLength;
            public uint ReturnLength;
            public NTSTATUS ResultCode;
            public Thread? Thread;
            public CancellationToken CancellationToken;
            public ManualResetEventSlim Started = new ManualResetEventSlim();
            public ManualResetEventSlim Complete = new ManualResetEventSlim();

            public void Dispose()
            {
                Started.Dispose();
                Complete.Dispose();
            }
        }

        private NtQueryObjectPool()
        {
            const int _threadCount = 2;
            _threads = new Thread[_threadCount];
            for (int i = 0; i < _threadCount; i++)
            {
                _threads[i] = new Thread(NtQueryObjectLoop);
                _threads[i].Start();
            }
        }

        public static readonly NtQueryObjectPool Instance = new NtQueryObjectPool();

        private readonly Concurrency.Semaphore _requestsAvailable = new Concurrency.Semaphore(0);
        private readonly ConcurrentQueue<ThreadRequest> _requests = new ConcurrentQueue<ThreadRequest>();
        private readonly Thread[] _threads;

        private void NtQueryObjectLoop()
        {
            try
            {
                while (true)
                {
                    _requestsAvailable.Wait(CancellationToken.None);
                    if (!_requests.TryDequeue(out var result))
                    {
                        continue;
                    }

                    // We know that ControlledExecution.Run is a hack, but fundamentally
                    // it is an OS bug that NtQueryObject can stall forever with no way
                    // of timing out. The only way to cancel the operation is to kill
                    // the thread.
                    result.Thread = Thread.CurrentThread;
#pragma warning disable SYSLIB0046
                    ControlledExecution.Run(() =>
                    {
                        result.Started.Set();
                        unsafe
                        {
                            uint returnLength = 0;
                            result.ResultCode = global::Windows.Win32.PInvoke.NtQueryObject(
                                new SafeFileHandle(result.Handle, false),
                                result.ObjectInformationClass,
                                (void*)result.ObjectInformation,
                                result.ObjectInformationLength,
                                &returnLength);
                            result.ReturnLength = returnLength;
                        }
                        result.Complete.Set();
                    }, result.CancellationToken);
#pragma warning restore SYSLIB0046
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore, we expect this to fire when NtQueryObject stalls.
            }
        }

        public NTSTATUS NtQueryObject(
            uint processId,
            HANDLE handle,
            OBJECT_INFORMATION_CLASS objectInformationClass,
            nint objectInformation,
            uint objectInformationLength,
            ref uint returnLength)
        {
            var cts = new CancellationTokenSource();
            using var request = new ThreadRequest
            {
                Handle = handle,
                ObjectInformationClass = objectInformationClass,
                ObjectInformation = objectInformation,
                ObjectInformationLength = objectInformationLength,
                CancellationToken = cts.Token,
            };
            _requests.Enqueue(request);
            _requestsAvailable.Release();

            // Wait an indefinite time for us to start being processed by the loop.
            request.Started.Wait();

            // Wait at most 50ms for NtQueryObject to actually finish.
            if (!request.Complete.Wait(50))
            {
                // NtQueryObject has stalled. Kill the thread and restart it.
                Debug.WriteLine($"Stalled requesting handle from process ID: {processId}");
                cts.Cancel();
                var i = Array.IndexOf(_threads, request.Thread);
                _threads[i] = new Thread(NtQueryObjectLoop);
                _threads[i].Start();
                returnLength = 0;
                return (NTSTATUS)NTSTATUSException.NT_STATUS_CANCELLED;
            }
            else
            {
                // NtQueryObject completed successfully. Return information from request.
                returnLength = request.ReturnLength;
                return request.ResultCode;
            }
        }
    }
}
