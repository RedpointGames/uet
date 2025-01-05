namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    /// <summary>
    /// Provides additional context when converting a Datastore value into a CLR value.
    /// </summary>
    internal class DatastoreValueConvertFromContext : ClrValueConvertFromContext
    {
        public required string ModelNamespace { get; init; }
    }
}
