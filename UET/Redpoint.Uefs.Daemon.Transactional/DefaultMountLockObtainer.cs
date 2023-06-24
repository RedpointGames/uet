namespace Redpoint.Uefs.Daemon.Transactional
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading.Tasks;

    internal class DefaultMountLockObtainer : IMountLockObtainer
    {
        private readonly ILogger<DefaultMountLockObtainer> _logger;

        public DefaultMountLockObtainer(
            ILogger<DefaultMountLockObtainer> logger)
        {
            _logger = logger;
        }

        private class CurrentOperationLockWrapper : IDisposable
        {
            private readonly ITransactionContext _context;
            private readonly IDisposable _wrappedLock;

            public CurrentOperationLockWrapper(
                ITransactionContext context,
                IDisposable wrappedLock)
            {
                _context = context;
                _wrappedLock = wrappedLock;
            }

            public void Dispose()
            {
                _context.CurrentMountOperation = null;
                _wrappedLock.Dispose();
            }
        }

        public async Task<IDisposable> ObtainLockAsync(
            ITransactionContext context,
            string operationName,
            CancellationToken cancellationToken)
        {
            var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
            IDisposable @lock;
            try
            {
                @lock = await context.ObtainLockAsync("mount-list", timeoutCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError($"Another mount operation has stalled for more than 30 seconds, preventing this request from being processed. This is a bug in UEFS. The current mount operation that is blocking is: {context.CurrentMountOperation}");
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, $"Another mount operation has stalled for more than 30 seconds, preventing this request from being processed. This is a bug in UEFS. The current mount operation that is blocking is: {context.CurrentMountOperation}"));
            }
            context.CurrentMountOperation = operationName;
            return new CurrentOperationLockWrapper(context, @lock);
        }
    }
}
