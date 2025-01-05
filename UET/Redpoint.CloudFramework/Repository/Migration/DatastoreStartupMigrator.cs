namespace Redpoint.CloudFramework.Repository.Migration
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DatastoreStartupMigrator : IHostedService
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly RegisteredModelMigratorBase[] _migrators;
        private readonly ILogger<DatastoreStartupMigrator> _logger;
        private readonly IGlobalLockService _globalLock;
        private readonly IGlobalPrefix _globalPrefix;

        public DatastoreStartupMigrator(
            IGlobalRepository globalRepository,
            IServiceProvider serviceProvider,
            RegisteredModelMigratorBase[] migrators,
            ILogger<DatastoreStartupMigrator> logger,
            IGlobalLockService globalLock,
            IGlobalPrefix globalPrefix)
        {
            _globalRepository = globalRepository;
            _serviceProvider = serviceProvider;
            _migrators = migrators;
            _logger = logger;
            _globalLock = globalLock;
            _globalPrefix = globalPrefix;
        }

        /// <summary>
        /// This isn't a real model; we just use it to construct keys for locking.
        /// </summary>
        private class RCFMigrationLockModel : Model
        {
            public override HashSet<string> GetIndexes()
            {
                throw new NotImplementedException();
            }

            public override string GetKind()
            {
                return "rcf_migrationLock";
            }

            public override long GetSchemaVersion()
            {
                throw new NotImplementedException();
            }

            public override Dictionary<string, FieldType> GetTypes()
            {
                throw new NotImplementedException();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_migrators.Length == 0)
            {
                _logger.LogInformation("There are no database migrators registered.");
                return;
            }
            else
            {
                _logger.LogInformation($"There are {_migrators.Length} database migrators registered.");
            }

            var drl = _serviceProvider.GetRequiredService<IDatastoreRepositoryLayer>();

            var migratorsByModel = _migrators.GroupBy(x => x.ModelType);
            foreach (var modelGroup in migratorsByModel)
            {
                _logger.LogInformation($"Running database migrators for: {modelGroup.Key.FullName}");

#pragma warning disable IL2072 // DynamicallyAccessedMembers is set on ModelType.
                var referenceModel = (Model)Activator.CreateInstance(modelGroup.Key)!;
#pragma warning restore IL2072
                var currentSchemaVersion = referenceModel.GetSchemaVersion();

                var migratorsByVersion = modelGroup.ToDictionary(k => k.ToSchemaVersion, v => v.MigratorType);
                for (long i = 2; i <= currentSchemaVersion; i++)
                {
                    if (!migratorsByVersion.ContainsKey(i))
                    {
                        throw new InvalidOperationException($"Missing migrator to migrate {modelGroup.Key.Name} from version {i - 1} to {i}. Make sure the migrator is registered in the service provider with .AddMigrator().");
                    }
                }
                var firstMigrator = modelGroup.First();

                // Do an early check before locking.
                if (!await firstMigrator.QueryForOutdatedModelsAsync(drl, currentSchemaVersion).AnyAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                // We must lock at the type level, because QueryForOutdatedModels will be out of date the moment
                // any individual model is processed.
                _logger.LogInformation($"Acquiring lock to perform migrations for '{referenceModel.GetKind()}'...");
                var keyFactory = await _globalRepository.GetKeyFactoryAsync<RCFMigrationLockModel>(string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
                var key = keyFactory.CreateKey(referenceModel.GetKind());
                var handler = await _globalLock.Acquire(string.Empty, key).ConfigureAwait(false);
                try
                {
                    _logger.LogInformation($"Performing migrations for '{referenceModel.GetKind()}'...");

                    await foreach (var loadedModel in firstMigrator.QueryForOutdatedModelsAsync(drl, currentSchemaVersion).ConfigureAwait(false))
                    {
                        var loadedModelVersion = loadedModel.schemaVersion ?? 1;
                        var needsSaveFromUs = false;

                        _logger.LogInformation($"Migrating '{_globalPrefix.CreateInternal(loadedModel.Key)}'...");

                        for (long i = loadedModelVersion + 1; i <= currentSchemaVersion; i++)
                        {
                            var migrator = _serviceProvider.GetService(migratorsByVersion[i]);
#pragma warning disable IL2075 // DynamicallyAccessedMembers is set on MigratorType.
                            var migrationDidSave = await ((Task<bool>)migratorsByVersion[i].GetMethod("MigrateAsync")!.Invoke(migrator, new object[] { loadedModel })!).ConfigureAwait(false);
#pragma warning restore IL2075
                            if (migrationDidSave)
                            {
                                needsSaveFromUs = false;
                                if (loadedModel.schemaVersion != i)
                                {
                                    throw new InvalidOperationException("Expected that MigrateAsync would set schemaVersion and call UpdateAsync as needed!");
                                }
                            }
                            else
                            {
                                needsSaveFromUs = true;
                                loadedModel.schemaVersion = i;
                            }
                        }

                        if (needsSaveFromUs)
                        {
                            await firstMigrator.UpdateAsync(_globalRepository, loadedModel).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to apply migrations for '{referenceModel.GetKind()}': {ex.Message}");
                }
                finally
                {
                    _logger.LogInformation($"Releasing lock that was used to perform migrations for '{referenceModel.GetKind()}'...");
                    await handler.DisposeAsync().ConfigureAwait(false);
                    _logger.LogInformation($"Released lock that was used to perform migrations for '{referenceModel.GetKind()}'.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
