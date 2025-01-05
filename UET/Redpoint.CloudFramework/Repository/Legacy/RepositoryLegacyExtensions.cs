namespace Redpoint.CloudFramework.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

#pragma warning disable CS0618 // Type or member is obsolete
    [Obsolete("These API methods are obsolete, and you should upgrade to the latest IRepository APIs.")]
    public static class RepositoryLegacyExtensions
    {
        private static DatastoreGlobalRepository G(IRepository repository)
        {
            return (DatastoreGlobalRepository)((DatastoreRepository)repository)._globalDatastore;
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<ModelQuery<T>> CreateQuery<T>(this IRepository repository) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).CreateQuery<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<MappedDatastoreQueryResults<T>> RunUncachedQuery<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IRepository repository, ModelQuery<T> query,
            ReadOptions.Types.ReadConsistency readConsistency, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).RunUncachedQuery(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), query, readConsistency, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use GetKeyFactoryAsync<T> instead.")]
        public static async Task<KeyFactory> GetKeyFactory<T>(this IRepository repository) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).GetKeyFactory<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Obsolete("Use LoadAsync<T> instead.")]
        public static async Task<Dictionary<Key, T?>> LoadMany<T>(this IRepository repository, Key[] keys, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadMany<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), keys, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use LoadAsync<T> instead.")]
        public static async Task<T?> LoadOneBy<T>(this IRepository repository, Key key, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadOneBy<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), key, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<T?> LoadOneBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TValue>(this IRepository repository, string field, TValue value, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadOneBy<T, TValue>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), field, value, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAllBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TValue>(this IRepository repository, string field, TValue value, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadAllBy<T, TValue>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), field, value, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAll<T>(this IRepository repository, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadAll<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), transaction).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAllUncached<T>(this IRepository repository, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).LoadAllUncached<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), transaction).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async IAsyncEnumerable<T> LoadAllByFiltersUncached<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IRepository repository, Filter filter) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            await foreach (var result in G(repository).LoadAllByFiltersUncached<T>(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), filter).ConfigureAwait(false))
            {
                yield return result;
            }
        }

        [Obsolete("Use CreateAsync<T> instead.")]
        public static async Task Create<T>(this IRepository repository, T model, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Create(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), model, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use CreateAsync<T> instead.")]
        public static async Task<T[]> CreateMany<T>(this IRepository repository, IList<T> models) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).CreateMany(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), models).ConfigureAwait(false);
        }

        [Obsolete("Use UpsertAsync<T> instead.")]
        public static async Task Upsert<T>(this IRepository repository, T model, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Upsert(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), model, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use UpdateAsync<T> instead.")]
        public static async Task Update<T>(this IRepository repository, T model, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Update(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), model, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use DeleteAsync<T> instead.")]
        public static async Task Delete<T>(this IRepository repository, T model, IModelTransaction? transaction = null) where T : Model, new()
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Delete(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), model, transaction).ConfigureAwait(false);
        }

        [Obsolete("Use BeginTransactionAsync instead.")]
        public static async Task<IModelTransaction> BeginTransaction(this IRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            return await G(repository).BeginTransaction(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Obsolete("Use CommitAsync instead.")]
        public static async Task Commit(this IRepository repository, IModelTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Commit(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), transaction).ConfigureAwait(false);
        }

        [Obsolete("Use RollbackAsync instead.")]
        public static async Task Rollback(this IRepository repository, IModelTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(repository);

            await G(repository).Rollback(await ((DatastoreRepository)repository).GetDatastoreNamespace().ConfigureAwait(false), transaction).ConfigureAwait(false);
        }
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
