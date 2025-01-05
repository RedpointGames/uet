namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    /// <summary>
    /// Provides additional context when converting a JSON value into a CLR value.
    /// </summary>
    internal class JsonValueConvertFromContext : ClrValueConvertFromContext
    {
        public required string ModelNamespace { get; init; }
    }
}
