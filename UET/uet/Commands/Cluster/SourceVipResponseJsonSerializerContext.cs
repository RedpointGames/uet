using System.Text.Json.Serialization;

namespace UET.Commands.Cluster
{
    [JsonSerializable(typeof(SourceVipResponse))]
    public partial class SourceVipResponseJsonSerializerContext : JsonSerializerContext
    {
    }
}
