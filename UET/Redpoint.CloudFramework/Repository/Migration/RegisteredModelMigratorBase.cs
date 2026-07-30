namespace Redpoint.CloudFramework.Repository.Migration
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal abstract class RegisteredModelMigratorBase
    {
        public abstract Type ModelType { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public required Type MigratorType { get; set; }

        public long ToSchemaVersion { get; set; }

        public abstract Type ExecutorType { get; }
    }
}
