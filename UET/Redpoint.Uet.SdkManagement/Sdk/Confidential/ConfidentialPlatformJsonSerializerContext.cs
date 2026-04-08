namespace Redpoint.Uet.SdkManagement
{
    using Redpoint.Uet.SdkManagement.Sdk.Confidential;
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(GenericPlatformConfig))]
    [JsonSerializable(typeof(ConfidentialPlatformConfig))]
    [JsonSerializable(typeof(ConfidentialPlatformAutoDiscovery))]
    public partial class ConfidentialPlatformJsonSerializerContext : JsonSerializerContext
    {
    }
}
