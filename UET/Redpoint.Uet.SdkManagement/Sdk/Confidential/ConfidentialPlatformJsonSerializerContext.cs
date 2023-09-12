namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ConfidentialPlatformConfig))]
    public partial class ConfidentialPlatformJsonSerializerContext : JsonSerializerContext
    {
    }
}
