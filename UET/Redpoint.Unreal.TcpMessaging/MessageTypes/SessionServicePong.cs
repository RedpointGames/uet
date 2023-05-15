namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/SessionMessages", "SessionServicePong")]
    public record class SessionServicePong
    {
        [JsonPropertyName("Authorized")]
        public bool Authorized = true;

        [JsonPropertyName("BuildDate")]
        public string BuildDate = string.Empty;

        [JsonPropertyName("DeviceName")]
        public string DeviceName = string.Empty;

        [JsonPropertyName("InstanceId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid InstanceId = Guid.Empty;

        [JsonPropertyName("InstanceName")]
        public string InstanceName = string.Empty;

        [JsonPropertyName("PlatformName")]
        public string PlatformName = string.Empty;

        [JsonPropertyName("SessionId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid SessionId = Guid.Empty;

        [JsonPropertyName("SessionName")]
        public string SessionName = string.Empty;

        [JsonPropertyName("SessionOwner")]
        public string SessionOwner = string.Empty;

        [JsonPropertyName("Standalone")]
        public bool Standalone = false;
    }
}
