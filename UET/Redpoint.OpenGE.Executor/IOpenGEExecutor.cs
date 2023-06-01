namespace Redpoint.OpenGE.Executor
{
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    public interface IOpenGEExecutor
    {
        Task<int> ExecuteAsync(CancellationTokenSource buildCancellationTokenSource);
    }
}
