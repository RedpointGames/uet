namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using System.Text.Json.Serialization;

    public record class AutomationExecutionEntry
    {
        [JsonPropertyName("Event")]
        public AutomationExecutionEntryEvent Event = new AutomationExecutionEntryEvent();

        [JsonPropertyName("Filename")]
        public string Filename = string.Empty;

        [JsonPropertyName("LineNumber")]
        public int LineNumber = 0;

        [JsonPropertyName("Timestamp")]
        public object Timestamp = new object();
    }
}
