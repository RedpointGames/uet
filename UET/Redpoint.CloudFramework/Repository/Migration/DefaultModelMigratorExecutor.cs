namespace Redpoint.CloudFramework.Repository.Migration
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultModelMigratorExecutor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IModelMigratorExecutor<T> where T : class, IModel, new()
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;
        private readonly ILogger<DefaultModelMigratorExecutor<T>> _logger;
        private readonly IGlobalLockService _globalLock;
        private readonly IGlobalPrefix _globalPrefix;
        private readonly IServiceProvider _serviceProvider;

        public DefaultModelMigratorExecutor(
            IGlobalRepository globalRepository,
            IDatastoreRepositoryLayer datastoreRepositoryLayer,
            ILogger<DefaultModelMigratorExecutor<T>> logger,
            IGlobalLockService globalLock,
            IGlobalPrefix globalPrefix,
            IServiceProvider serviceProvider)
        {
            _globalRepository = globalRepository;
            _datastoreRepositoryLayer = datastoreRepositoryLayer;
            _logger = logger;
            _globalLock = globalLock;
            _globalPrefix = globalPrefix;
            _serviceProvider = serviceProvider;
        }

        private async Task UpdateAsync(T model)
        {
            await _globalRepository.UpdateAsync(string.Empty, model, null, null, CancellationToken.None).ConfigureAwait(false);
        }

        private IBatchedAsyncEnumerable<T> QueryForOutdatedModelsAsync(
            long currentSchemaVersion)
        {
            return _datastoreRepositoryLayer.QueryAsync<T>(
                string.Empty,
                x => x.schemaVersion < currentSchemaVersion,
                null,
                null,
                null,
                null,
                CancellationToken.None);
        }

        public async Task ExecuteMigratorsAsync(
            RegisteredModelMigrator<T>[] migrators,
            CancellationToken cancellationToken)
        {
            var referenceModel = new T();
            var currentSchemaVersion = referenceModel.GetSchemaVersion();

            var migratorsByVersion = migrators.ToDictionary(k => k.ToSchemaVersion, v => v.MigratorType);
            for (long i = 2; i <= currentSchemaVersion; i++)
            {
                if (!migratorsByVersion.ContainsKey(i))
                {
                    throw new InvalidOperationException($"Missing migrator to migrate {typeof(T).Name} from version {i - 1} to {i}. Make sure the migrator is registered in the service provider with .AddMigrator().");
                }
            }
            var firstMigrator = migrators.First();

            // Do an early check before locking.
            if (!await QueryForOutdatedModelsAsync(currentSchemaVersion).AnyAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                return;
            }

        retryLock:
            // We must lock at the type level, because QueryForOutdatedModels will be out of date the moment
            // any individual model is processed.
            try
            {
                _logger.LogInformation($"Acquiring lock to perform migrations for '{referenceModel.GetKind()}'...");
                var keyFactory = await _globalRepository.GetKeyFactoryAsync<MigrationLockModel>(string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
                var key = keyFactory.CreateKey(referenceModel.GetKind());
                var handler = await _globalLock.Acquire(string.Empty, key).ConfigureAwait(false);
                try
                {
                    _logger.LogInformation($"Performing migrations for '{referenceModel.GetKind()}'...");

                    await foreach (var initiallyLoadedModel in QueryForOutdatedModelsAsync(currentSchemaVersion).ConfigureAwait(false))
                    {
                        var loadedModelVersion = initiallyLoadedModel.schemaVersion ?? 1;
                        var needsSaveFromUs = false;

                        var model = initiallyLoadedModel;
                        for (long i = loadedModelVersion + 1; i <= currentSchemaVersion; i++)
                        {
                            _logger.LogInformation($"Migrating '{_globalPrefix.CreateInternal(initiallyLoadedModel.Key)}' from schema version {i - 1} to {i}...");

                            var migrator = _serviceProvider.GetService(migratorsByVersion[i]);
                            if (migrator is ITransactionalModelMigrator<T> transactionalMigrator)
                            {
                                if (needsSaveFromUs)
                                {
                                    // Non-transactional migrators also ran and haven't saved yet.
                                    await UpdateAsync(model).ConfigureAwait(false);
                                    needsSaveFromUs = false;
                                }

                            retryLoad:
                                // Attempt to reload the model within a transaction, and make sure it's schema version is exactly
                                // the previous version (so we know the version we loaded had all of the previous migrators apply).
                                await using (var transaction = await _globalRepository.BeginTransactionAsync(
                                    string.Empty,
                                    Transaction.TransactionMode.ReadWrite,
                                    cancellationToken: cancellationToken).ConfigureAwait(false))
                                {
                                    var reloadedModel = await _globalRepository.LoadAsync<T>(
                                        string.Empty,
                                        model.Key,
                                        transaction,
                                        cancellationToken: cancellationToken);
                                    if (reloadedModel == null)
                                    {
                                        throw new InvalidOperationException($"Model {_globalPrefix.CreateInternal(model.Key)} did not exist after migration!");
                                    }

                                    if (reloadedModel.schemaVersion < i - 1)
                                    {
                                        _logger.LogWarning($"Model {_globalPrefix.CreateInternal(model.Key)} should be at schema version {i - 1} prior to transactional migration, but was at schema version {reloadedModel.schemaVersion} in order to progress to schema version {i}. This is likely a delay in the previous migration applying to Datastore and the subsequent reload within a transaction. Delaying by 1 second and then reloading the model again.");
                                        await Task.Delay(1000, cancellationToken);
                                        goto retryLoad;
                                    }
                                    else if (reloadedModel.schemaVersion > i - 1)
                                    {
                                        throw new InvalidOperationException("Another operation updated the schema version of this model, but the migration lock should have prevented this.");
                                    }

                                    // We're now exactly at i-1, and ready to proceed to i.
                                    await transactionalMigrator.MigrateAsync(
                                        reloadedModel,
                                        transaction);

                                    if (reloadedModel.schemaVersion != i)
                                    {
                                        throw new InvalidOperationException($"Model migrator {transactionalMigrator.GetType().FullName} did not update schema version to {i} on provided model.");
                                    }

                                    // Update loadedModel to point at the model we just modified, so that subsequent non-transactional
                                    // migrators see the correct version.
                                    model = reloadedModel;
                                }
                            }
                            else if (migrator is INonTransactionalModelMigrator<T> nonTransactionalModelMigrator)
                            {
                                var nonTransactionalResult = await nonTransactionalModelMigrator
                                    .MigrateAsync(model)
                                    .ConfigureAwait(false);
                                if (nonTransactionalResult == NonTransactionalModelMigrationResult.Updated)
                                {
                                    needsSaveFromUs = false;
                                    if (model.schemaVersion != i)
                                    {
                                        throw new InvalidOperationException($"Model migrator {nonTransactionalModelMigrator.GetType().FullName} did not update schema version to {i} on provided model.");
                                    }
                                }
                                else
                                {
                                    needsSaveFromUs = true;
                                    model.schemaVersion = i;
                                }
                            }
                        }

                        if (needsSaveFromUs)
                        {
                            await UpdateAsync(model).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex) when (!ex.GetType().FullName!.StartsWith("Xunit.", StringComparison.Ordinal))
                {
                    _logger.LogError(ex, $"'{ex.GetType().Name}': Failed to apply migrations for '{referenceModel.GetKind()}': {ex.Message}");
                }
                finally
                {
                    _logger.LogInformation($"Releasing lock that was used to perform migrations for '{referenceModel.GetKind()}'...");
                    await handler.DisposeAsync().ConfigureAwait(false);
                    _logger.LogInformation($"Released lock that was used to perform migrations for '{referenceModel.GetKind()}'.");
                }
            }
            catch (LockAcquisitionException)
            {
                _logger.LogWarning($"Unable to acquire lock for database migration. Waiting 20 seconds and then trying again...");
                await Task.Delay(20000, cancellationToken);
                goto retryLock;
            }
        }
    }
}
