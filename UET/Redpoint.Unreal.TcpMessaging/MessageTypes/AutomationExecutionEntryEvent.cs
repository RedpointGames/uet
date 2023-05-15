namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    public record class AutomationExecutionEntryEvent
    {
        [JsonPropertyName("Type")]
        public string Type = string.Empty;

        [JsonPropertyName("Message")]
        public string Message = string.Empty;

        [JsonPropertyName("Context")]
        public string Context = string.Empty;

        [JsonPropertyName("Artifact"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid Artifact = Guid.Empty;
    }
}
