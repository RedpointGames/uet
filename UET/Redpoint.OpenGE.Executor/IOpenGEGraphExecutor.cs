namespace Redpoint.OpenGE.Executor
{
    using System.Threading.Tasks;

    public interface IOpenGEGraphExecutor
    {
        bool CancelledDueToFailure { get; }

        Task<int> ExecuteAsync(CancellationTokenSource buildCancellationTokenSource);
    }
}
