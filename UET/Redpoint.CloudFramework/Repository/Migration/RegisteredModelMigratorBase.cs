namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal abstract class RegisteredModelMigratorBase
    {
        public abstract Type ModelType { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public required Type MigratorType { get; set; }

        public long ToSchemaVersion { get; set; }

        public abstract Task UpdateAsync(IGlobalRepository globalRepository, object model);

        public abstract IAsyncEnumerable<Model> QueryForOutdatedModelsAsync(IDatastoreRepositoryLayer drl, long currentSchemaVersion);
    }
}
