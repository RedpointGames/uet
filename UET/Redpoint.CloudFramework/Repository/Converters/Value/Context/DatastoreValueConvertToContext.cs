namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    using Redpoint.CloudFramework.Models;
    using Google.Cloud.Datastore.V1;

    /// <summary>
    /// Provides additional context when a CLR value into a Datastore value.
    /// </summary>
    internal class DatastoreValueConvertToContext : ClrValueConvertFromContext
    {
        public required string ModelNamespace { get; init; }

        public required IModel Model { get; init; }

        public required Entity Entity { get; init; }
    }
}
