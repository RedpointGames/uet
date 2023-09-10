namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Grpc.Core;
    using System.Threading.Tasks;

    internal class GuardedResponseStream<T> : IGuardedResponseStream<T>
    {
        private readonly IServerStreamWriter<T> _responseStream;
        private readonly SemaphoreSlim _semaphore;

        public GuardedResponseStream(IServerStreamWriter<T> responseStream)
        {
            _responseStream = responseStream;
            _semaphore = new SemaphoreSlim(1);
        }

        public async Task WriteAsync(
            T response,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync();
            try
            {
                await _responseStream.WriteAsync(response);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
