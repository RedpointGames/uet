namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(TrueNasQueryOptions))]
    [JsonSerializable(typeof(TrueNasQuery))]
    [JsonSerializable(typeof(TrueNasSnapshot[]))]
    [JsonSerializable(typeof(TrueNasSnapshotClone))]
    [JsonSerializable(typeof(TrueNasDeleteOptions))]
    [JsonSerializable(typeof(TrueNasDataset))]
    [JsonSerializable(typeof(RemoteZfsServerConfig))]
    [JsonSerializable(typeof(RemoteZfsServerConfigTemplate))]
    internal partial class RemoteZfsSerializerContext : JsonSerializerContext
    {
    }
}
