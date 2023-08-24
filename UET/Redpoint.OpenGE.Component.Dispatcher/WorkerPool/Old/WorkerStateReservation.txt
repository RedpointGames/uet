namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;

    internal class WorkerStateReservation
    {
        /// <summary>
        /// The duplex streaming call that is brought over from <see cref="WorkerStatePendingReservation"/> when a core is successfully reserved. This is the channel that the implementation of <see cref="IWorkerCore"/> performs operations on.
        /// </summary>
        public required AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request { get; set; }

        /// <summary>
        /// The instance of <see cref="IWorkerCore"/> that <see cref="IWorkerPool.ReserveRemoteOrLocalCoreAsync(CancellationToken)"/> will return.
        /// </summary>
        public required IWorkerCore Core { get; set; }

        /// <summary>
        /// The cancellation token source that can be used to cancel the associated request.
        /// </summary>
        public required CancellationTokenSource CancellationTokenSource { get; set; }
    }
}
