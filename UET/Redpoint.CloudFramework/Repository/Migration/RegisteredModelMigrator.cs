namespace Redpoint.CloudFramework.Repository.Migration
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    internal sealed class RegisteredModelMigrator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : RegisteredModelMigratorBase where T : class, IModel, new()
    {
        public override Type ModelType { get; } = typeof(T);

        public override Type ExecutorType { get; } = typeof(IModelMigratorExecutor<T>);
    }
}
