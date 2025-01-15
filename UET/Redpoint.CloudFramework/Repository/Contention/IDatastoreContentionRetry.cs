namespace Redpoint.CloudFramework.Repository.Contention
{
    using System;
    using System.Threading.Tasks;

    public interface IDatastoreContentionRetry
    {
        Task RunWithContentionRetryAsync(
            Func<Task> logic,
            CancellationToken cancellationToken);

        Task<T> RunWithContentionRetryAsync<T>(
            Func<Task<T>> logic,
            CancellationToken cancellationToken);
    }
}
