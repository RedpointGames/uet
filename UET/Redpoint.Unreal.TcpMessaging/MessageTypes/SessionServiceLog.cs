namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/SessionMessages", "SessionServiceLog")]
    public record class SessionServiceLog
    {
        [JsonPropertyName("Category")]
        public string Category = string.Empty;

        [JsonPropertyName("Data")]
        public string Data = string.Empty;

        [JsonPropertyName("InstanceId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid InstanceId = Guid.Empty;

        [JsonPropertyName("TimeSeconds")]
        public double TimeSeconds = 0.0;

        [JsonPropertyName("Verbosity")]
        public byte Verbosity = 4; // Display
    }
}
