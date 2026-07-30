namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal sealed class RegisteredModelMigrator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : RegisteredModelMigratorBase where T : class, IModel, new()
    {
        public override Type ModelType { get; } = typeof(T);

        public override Type ExecutorType { get; } = typeof(IModelMigratorExecutor<T>);
    }
}
