namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    using Redpoint.CloudFramework.Models;

    /// <summary>
    /// Provides additional context when a CLR value into a JSON value.
    /// </summary>
    internal class JsonValueConvertToContext : ClrValueConvertFromContext
    {
        public required string ModelNamespace { get; init; }

        public required Model Model { get; init; }
    }
}
