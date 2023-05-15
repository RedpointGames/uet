namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/EngineMessages", "EngineServicePong")]
    public record class EngineServicePong
    {
        [JsonPropertyName("CurrentLevel")]
        public string CurrentLevel = string.Empty;

        [JsonPropertyName("EngineVersion")]
        public int EngineVersion = 0;

        [JsonPropertyName("HasBegunPlay")]
        public bool HasBegunPlay = false;

        [JsonPropertyName("InstanceId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid InstanceId = Guid.Empty;

        [JsonPropertyName("InstanceType")]
        public string InstanceType = string.Empty;

        [JsonPropertyName("SessionId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid SessionId = Guid.Empty;

        [JsonPropertyName("WorldTimeSeconds")]
        public float WorldTimeSeconds = 0.0f;
    }
}
