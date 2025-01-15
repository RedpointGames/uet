namespace Redpoint.CloudFramework.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Google.Protobuf.Collections;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.Collections;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    [Obsolete("These API methods are obsolete, and you should upgrade to the latest IRepository APIs.")]
    public static class GlobalRepositoryLegacyExtensions
    {
        private static IRepositoryLayer R(IGlobalRepository globalRepository)
        {
            return ((DatastoreGlobalRepository)globalRepository).Layer;
        }

        private static Expression ConvertFilterToExpression<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(IGlobalRepository globalRepository, Filter filter, ParameterExpression parameterExpr)
        {
            if (filter == null)
            {
                return Expression.Constant(true);
            }
            else if (filter.FilterTypeCase == Filter.FilterTypeOneofCase.PropertyFilter)
            {
                var targetProperty = typeof(T).GetProperty(filter.PropertyFilter.Property.Name == "__key__" ? "Key" : filter.PropertyFilter.Property.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetProperty == null)
                {
                    throw new InvalidOperationException($"Unable to get target property named '{filter.PropertyFilter.Property.Name}' on type '{typeof(T).FullName}'");
                }
                var memberAccessExpr = Expression.MakeMemberAccess(parameterExpr, targetProperty);
                Expression constantExpr;
                if (filter.PropertyFilter.Value.ValueTypeCase == Value.ValueTypeOneofCase.TimestampValue)
                {
                    constantExpr = Expression.Constant(((DatastoreGlobalRepository)globalRepository)._instantTimestampConverter.FromDatastoreValueToNodaTimeInstant(filter.PropertyFilter.Value), typeof(Instant?));
                }
                else
                {
                    constantExpr = Expression.Convert(Expression.Constant(filter.PropertyFilter.Value), targetProperty.PropertyType);
                }

                switch (filter.PropertyFilter.Op)
                {
                    case PropertyFilter.Types.Operator.LessThan:
                        return Expression.MakeBinary(ExpressionType.LessThan, memberAccessExpr, constantExpr);
                    case PropertyFilter.Types.Operator.LessThanOrEqual:
                        return Expression.MakeBinary(ExpressionType.LessThanOrEqual, memberAccessExpr, constantExpr);
                    case PropertyFilter.Types.Operator.GreaterThan:
                        return Expression.MakeBinary(ExpressionType.GreaterThan, memberAccessExpr, constantExpr);
                    case PropertyFilter.Types.Operator.GreaterThanOrEqual:
                        return Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, memberAccessExpr, constantExpr);
                    case PropertyFilter.Types.Operator.Equal:
                        return Expression.MakeBinary(ExpressionType.Equal, memberAccessExpr, constantExpr);
                    case PropertyFilter.Types.Operator.HasAncestor:
                        return Expression.Call(
                            null,
                            typeof(RepositoryExtensions).GetMethod(nameof(RepositoryExtensions.HasAncestor))!,
                            memberAccessExpr,
                            constantExpr);
                    default:
                        throw new InvalidOperationException();
                }
            }
            else
            {
                Expression chainedExpr = ConvertFilterToExpression<T>(globalRepository, filter.CompositeFilter.Filters[0], parameterExpr);
                for (int i = 1; i < filter.CompositeFilter.Filters.Count; i++)
                {
                    chainedExpr = Expression.AndAlso(chainedExpr, ConvertFilterToExpression<T>(globalRepository, filter.CompositeFilter.Filters[1], parameterExpr));
                }
                return chainedExpr;
            }
        }

        private static Expression<Func<T, bool>>? ConvertOrderToExpression<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(IGlobalRepository globalRepository, RepeatedField<PropertyOrder> order, ParameterExpression parameterExpr)
        {
            if (order.Count == 0)
            {
                return null;
            }
            else
            {
                Expression? chainedExpr = null;
                for (int i = 0; i < order.Count; i++)
                {
                    var targetProperty = typeof(T).GetProperty(order[i].Property.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (targetProperty == null)
                    {
                        throw new InvalidOperationException($"Unable to get target property named '{order[i].Property.Name}' on type '{typeof(T).FullName}'");
                    }
                    var memberAccessExpr = Expression.MakeMemberAccess(parameterExpr, targetProperty);
                    var orderExpr = order[i].Direction == PropertyOrder.Types.Direction.Ascending ?
                        Expression.MakeBinary(ExpressionType.LessThan, memberAccessExpr, memberAccessExpr) :
                        Expression.MakeBinary(ExpressionType.GreaterThan, memberAccessExpr, memberAccessExpr);
                    if (chainedExpr == null)
                    {
                        chainedExpr = orderExpr;
                    }
                    else
                    {
                        chainedExpr = Expression.MakeBinary(ExpressionType.Or, chainedExpr, orderExpr);
                    }
                }
                return Expression.Lambda<Func<T, bool>>(chainedExpr!, parameterExpr);
            }
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static Task<ModelQuery<T>> CreateQuery<T>(this IGlobalRepository globalRepository, string @namespace) where T : class, IModel, new()
        {
            return Task.FromResult(new ModelQuery<T>(@namespace, new Query(new T().GetKind())));
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<MappedDatastoreQueryResults<T>> RunUncachedQuery<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IGlobalRepository globalRepository, string @namespace, ModelQuery<T> query,
            ReadOptions.Types.ReadConsistency readConsistency, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);
            ArgumentNullException.ThrowIfNull(query);

            var parameterExpr = Expression.Parameter(typeof(T));
            var conditionExpr = ConvertFilterToExpression<T>(globalRepository, query.Query.Filter, parameterExpr);
            var expr = Expression.Lambda<Func<T, bool>>(conditionExpr, parameterExpr);

            var order = ConvertOrderToExpression<T>(globalRepository, query.Query.Order, parameterExpr);

            var results = await R(globalRepository).QueryAsync(@namespace, expr, order, query.Query.Limit, transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(false);

            return new MappedDatastoreQueryResults<T>
            {
                EndCursor = ByteString.Empty,
                EndCursorForClients = null,
                Entities = results,
                MoreResults = QueryResultBatch.Types.MoreResultsType.NoMoreResults,
            };
        }

        [Obsolete("Use GetKeyFactoryAsync<T> instead.")]
        public static async Task<KeyFactory> GetKeyFactory<T>(this IGlobalRepository globalRepository, string @namespace) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).GetKeyFactoryAsync<T>(@namespace, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use LoadAsync<T> instead.")]
        public static async Task<Dictionary<Key, T?>> LoadMany<T>(this IGlobalRepository globalRepository, string @namespace, Key[] keys, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).LoadAsync<T>(@namespace, keys.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ToSafeDictionaryAsync(k => k.Key, v => v.Value).ConfigureAwait(false);
        }

        [Obsolete("Use LoadAcrossNamespacesAsync<T> instead.")]
        public static async Task<Dictionary<Key, T?>> LoadManyAcrossNamespaces<T>(this IGlobalRepository globalRepository, Key[] keys) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).LoadAcrossNamespacesAsync<T>(keys.ToAsyncEnumerable(), null, CancellationToken.None).ToSafeDictionaryAsync(k => k.Key, v => v.Value).ConfigureAwait(false);
        }

        [Obsolete("Use LoadAsync<T> instead.")]
        public static async Task<T?> LoadOneBy<T>(this IGlobalRepository globalRepository, string @namespace, Key key, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).LoadAsync<T>(@namespace, key, transaction, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<T?> LoadOneBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TValue>(this IGlobalRepository globalRepository, string @namespace, string field, TValue value, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            var parameterExpr = Expression.Parameter(typeof(T));
            var targetProperty = typeof(T).GetProperty(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (targetProperty == null)
            {
                throw new InvalidOperationException($"Unable to get target property named '{field}' on type '{typeof(T).FullName}'");
            }
            var accessExpr = Expression.MakeMemberAccess(parameterExpr, targetProperty);
            BinaryExpression equalExpr;
            if (value != null && accessExpr.Type.Name.StartsWith("Nullable`1", StringComparison.Ordinal) && !value.GetType().Name.StartsWith("Nullable`1", StringComparison.Ordinal))
            {
                equalExpr = Expression.Equal(accessExpr, Expression.Convert(Expression.Constant(value), accessExpr.Type));
            }
            else
            {
                equalExpr = Expression.Equal(accessExpr, Expression.Constant(value));
            }
            var expr = Expression.Lambda<Func<T, bool>>(equalExpr, parameterExpr);

            return await R(globalRepository).QueryAsync(@namespace, expr, null, 1, transaction, null, CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAllBy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, TValue>(this IGlobalRepository globalRepository, string @namespace, string field, TValue? value, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            var parameterExpr = Expression.Parameter(typeof(T));
            var targetProperty = typeof(T).GetProperty(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (targetProperty == null)
            {
                throw new InvalidOperationException($"Unable to get target property named '{field}' on type '{typeof(T).FullName}'");
            }
            var accessExpr = Expression.MakeMemberAccess(parameterExpr, targetProperty);
            BinaryExpression equalExpr;
            if (value != null && accessExpr.Type.Name.StartsWith("Nullable`1", StringComparison.Ordinal) && !value.GetType().Name.StartsWith("Nullable`1", StringComparison.Ordinal))
            {
                equalExpr = Expression.Equal(accessExpr, Expression.Convert(Expression.Constant(value), accessExpr.Type));
            }
            else
            {
                equalExpr = Expression.Equal(accessExpr, Expression.Constant(value));
            }
            var expr = Expression.Lambda<Func<T, bool>>(equalExpr, parameterExpr);

            return await R(globalRepository).QueryAsync(@namespace, expr, null, null, transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAll<T>(this IGlobalRepository globalRepository, string @namespace, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).QueryAsync<T>(@namespace, x => true, null, null, transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async Task<List<T>> LoadAllUncached<T>(this IGlobalRepository globalRepository, string @namespace, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            // NOTE: This should forcibly use the datastore repository layer instead of the redis cache repository layer, but also
            // we should just delete this method because it's no longer necessary.
            return await R(globalRepository).QueryAsync<T>(@namespace, x => true, null, null, transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(false);
        }

        [Obsolete("Use QueryAsync<T> instead.")]
        public static async IAsyncEnumerable<T> LoadAllByFiltersUncached<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IGlobalRepository globalRepository, string @namespace, Filter filter) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            var parameterExpr = Expression.Parameter(typeof(T));
            var conditionExpr = ConvertFilterToExpression<T>(globalRepository, filter, parameterExpr);
            var expr = Expression.Lambda<Func<T, bool>>(conditionExpr, parameterExpr);

            await foreach (var model in R(globalRepository).QueryAsync(@namespace, expr, null, null, null, null, CancellationToken.None).ConfigureAwait(false))
            {
                yield return model;
            }
        }

        [Obsolete("Use CreateAsync<T> instead.")]
        public static async Task Create<T>(this IGlobalRepository globalRepository, string @namespace, T model, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).CreateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
        }

        [Obsolete("Use CreateAsync<T> instead.")]
        public static async Task<T[]> CreateMany<T>(this IGlobalRepository globalRepository, string @namespace, IList<T> models) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).CreateAsync(@namespace, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(false);
        }

        [Obsolete("Use UpsertAsync<T> instead.")]
        public static async Task Upsert<T>(this IGlobalRepository globalRepository, string @namespace, T model, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).UpsertAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
        }

        [Obsolete("Use UpdateAsync<T> instead.")]
        public static async Task Update<T>(this IGlobalRepository globalRepository, string @namespace, T model, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).UpdateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(false);
        }

        [Obsolete("Use UpdateAsync<T> instead.")]
        public static async Task UpdateMany<T>(this IGlobalRepository globalRepository, string @namespace, IList<T> models) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).UpdateAsync(@namespace, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(false);
        }

        [Obsolete("Use DeleteAsync<T> instead.")]
        public static async Task Delete<T>(this IGlobalRepository globalRepository, string @namespace, T model, IModelTransaction? transaction = null) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).DeleteAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use DeleteAsync<T> instead.")]
        public static async Task DeleteMany<T>(this IGlobalRepository globalRepository, string @namespace, IList<T> models) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).DeleteAsync(@namespace, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use GetKeyFactoryAsync<T> and create the named key manually.")]
        public static async Task<Key> CreateNamedKey<T>(this IGlobalRepository globalRepository, string @namespace, string name) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            var factory = await R(globalRepository).GetKeyFactoryAsync<T>(@namespace, null, CancellationToken.None).ConfigureAwait(false);
            return factory.CreateKey(name);
        }

        [Obsolete("Use BeginTransactionAsync instead.")]
        public static async Task<IModelTransaction> BeginTransaction(this IGlobalRepository globalRepository, string @namespace)
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            return await R(globalRepository).BeginTransactionAsync(@namespace, TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use CommitAsync instead.")]
        public static async Task Commit(this IGlobalRepository globalRepository, string @namespace, IModelTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).CommitAsync(@namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
        }

        [Obsolete("Use RollbackAsync instead.")]
        public static async Task Rollback(this IGlobalRepository globalRepository, string @namespace, IModelTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            await R(globalRepository).RollbackAsync(@namespace, transaction, null, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
