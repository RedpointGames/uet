namespace Redpoint.Uet.Database
{
    using Redpoint.Uet.Database.Models;

    public interface IUetDbConnection : IAsyncDisposable
    {
        Task CreateAsync<T>(
            T value,
            CancellationToken cancellationToken) where T : IUetModel, new();

        Task<T?> FindAsync<T>(
            string key,
            CancellationToken cancellationToken) where T : IUetModel, new();
    }
}
