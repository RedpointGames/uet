namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Docker.Registry.DotNet.Models;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ManifestLayer))]
    public partial class PackageFsJsonSerializerContext : JsonSerializerContext
    {
    }
}
