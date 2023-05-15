namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/PortalRpc", "PortalRpcLocateServer")]
    public record class PortalRpcLocateServer
    {
        [JsonPropertyName("ProductId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid ProductId = Guid.Empty;

        [JsonPropertyName("ProductVersion")]
        public string ProductVersion = string.Empty;

        [JsonPropertyName("HostMacVersion")]
        public string HostMacVersion = string.Empty;

        [JsonPropertyName("HostUserId")]
        public string HostUserId = string.Empty;
    }
}
