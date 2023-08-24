namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WorkerStatePendingReservation
    {
        /// <summary>
        /// Our current duplex streaming call that we are performing or using the reservation on.
        /// </summary>
        public AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse>? Request { get; set; }

        /// <summary>
        /// The asynchronous task that is managing the reservation process with this remote worker.
        /// </summary>
        public Task? Task { get; set; }

        /// <summary>
        /// The cancellation token that is used to cancel the pending reservation of the remote worker.
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource { get; set; }
    }
}
