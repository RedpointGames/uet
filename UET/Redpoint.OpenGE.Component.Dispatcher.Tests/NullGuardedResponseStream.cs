namespace Redpoint.OpenGE.Component.Dispatcher.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    public partial class GraphExecutionTests
    {
        internal class NullGuardedResponseStream<T> : IGuardedResponseStream<T>
        {
            public Task WriteAsync(T response, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}