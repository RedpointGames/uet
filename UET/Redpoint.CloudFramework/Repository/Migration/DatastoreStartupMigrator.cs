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

                RegisteredModelMigratorBase[] migrators = modelGroup.ToArray();

                var executor = _serviceProvider.GetService(modelGroup.First().ExecutorType) as IModelMigratorExecutor;
                if (executor == null)
                {
                    _logger.LogError($"Missing database migrator executor implementation for {modelGroup.Key.FullName}!");
                }
                else
                {
                    await executor.ExecuteMigratorsAsync(migrators, cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
