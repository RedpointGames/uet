namespace Redpoint.OpenGE.Executor
{
    using System.Threading.Tasks;

    public interface IOpenGEExecutor
    {
        bool CancelledDueToFailure { get; }

        Task<int> ExecuteAsync(CancellationTokenSource buildCancellationTokenSource);
    }
}
