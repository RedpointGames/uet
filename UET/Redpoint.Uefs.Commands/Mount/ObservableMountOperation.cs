namespace Redpoint.Uefs.Commands.Mount
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Protocol;

    internal class ObservableMountOperation<TRequest> : ObservableOperation<TRequest, MountResponse>
    {
        public ObservableMountOperation(
            IRetryableGrpc retryableGrpc,
            IMonitorFactory monitorFactory,
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<MountResponse>> call,
            TRequest request,
            TimeSpan idleTimeout,
            CancellationToken cancellationToken) : base(
                retryableGrpc,
                monitorFactory,
                call,
                response => response.PollingResponse,
                request,
                idleTimeout,
                cancellationToken)
        {
        }

        public async Task<string> RunAndWaitForMountIdAsync()
        {
            return (await RunAndWaitForCompleteAsync().ConfigureAwait(false)).MountId;
        }
    }
}
