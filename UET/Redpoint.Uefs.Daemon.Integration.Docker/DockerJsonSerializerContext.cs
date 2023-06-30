namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    using Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(EmptyRequest))]
    [JsonSerializable(typeof(DockerCapabilitiesInfo))]
    [JsonSerializable(typeof(DockerCapabilitiesResponse))]
    [JsonSerializable(typeof(DockerCreateRequest))]
    [JsonSerializable(typeof(DockerCreateResponse))]
    [JsonSerializable(typeof(DockerGetRequest))]
    [JsonSerializable(typeof(DockerGetResponse))]
    [JsonSerializable(typeof(DockerHandshakeResponse))]
    [JsonSerializable(typeof(DockerListResponse))]
    [JsonSerializable(typeof(DockerMountRequest))]
    [JsonSerializable(typeof(DockerMountResponse))]
    [JsonSerializable(typeof(DockerPathRequest))]
    [JsonSerializable(typeof(DockerPathResponse))]
    [JsonSerializable(typeof(DockerRemoveRequest))]
    [JsonSerializable(typeof(DockerRemoveResponse))]
    [JsonSerializable(typeof(DockerUnmountRequest))]
    [JsonSerializable(typeof(DockerUnmountResponse))]
    [JsonSerializable(typeof(DockerVolumeInfo))]
    [JsonSerializable(typeof(GenericErrorResponse))]
    public partial class DockerJsonSerializerContext : JsonSerializerContext
    {
    }
}
