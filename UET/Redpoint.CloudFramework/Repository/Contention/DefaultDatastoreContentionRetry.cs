namespace Redpoint.CloudFramework.Repository.Contention
{
    using Grpc.Core;
    using System;
    using System.Threading.Tasks;

    internal class DefaultDatastoreContentionRetry : IDatastoreContentionRetry
    {
        public async Task RunWithContentionRetryAsync(
            Func<Task> logic,
            CancellationToken cancellationToken)
        {
        retry:
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await logic().ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.IsContentionException())
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                goto retry;
            }
        }

        public async Task<T> RunWithContentionRetryAsync<T>(
            Func<Task<T>> logic,
            CancellationToken cancellationToken)
        {
        retry:
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await logic().ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.IsContentionException())
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                goto retry;
            }
        }
    }
}
