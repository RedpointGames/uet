namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    public interface IGraphExecutor
    {
        bool CancelledDueToFailure { get; }

        Task ExecuteAsync(
            IAsyncStreamWriter<JobResponse> responseStream,
            CancellationTokenSource buildCancellationTokenSource);
    }
}
