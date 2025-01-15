namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    internal sealed class RegisteredModelMigrator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : RegisteredModelMigratorBase where T : class, IModel, new()
    {
        public override Type ModelType { get; } = typeof(T);

        public override async Task UpdateAsync(IGlobalRepository globalRepository, object model)
        {
            await globalRepository.UpdateAsync(string.Empty, (T)model, null, null, CancellationToken.None).ConfigureAwait(false);
        }

        public override IAsyncEnumerable<IModel> QueryForOutdatedModelsAsync(IDatastoreRepositoryLayer drl, long currentSchemaVersion)
        {
            return drl.QueryAsync<T>(
                string.Empty,
                x => x.schemaVersion < currentSchemaVersion,
                null,
                null,
                null,
                null,
                CancellationToken.None).Cast<IModel>();
        }
    }
}
