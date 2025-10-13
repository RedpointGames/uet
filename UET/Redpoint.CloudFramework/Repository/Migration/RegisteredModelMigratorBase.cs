namespace Redpoint.CloudFramework.Repository.Migration
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Transaction;
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
