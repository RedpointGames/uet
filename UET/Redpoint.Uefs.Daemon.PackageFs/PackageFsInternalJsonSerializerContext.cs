namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Redpoint.Uefs.Daemon.PackageFs.CachingStorage;
    using Redpoint.Uefs.Daemon.PackageFs.Tagging;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(PackageStorageTag))]
    [JsonSerializable(typeof(CachingInfoJson))]
    internal partial sealed class PackageFsInternalJsonSerializerContext : JsonSerializerContext
    {
    }
}
