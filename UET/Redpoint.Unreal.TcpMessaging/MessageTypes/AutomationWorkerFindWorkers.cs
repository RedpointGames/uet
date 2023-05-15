namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerFindWorkers")]
    public record class AutomationWorkerFindWorkers
    {
        [JsonPropertyName("Changelist")]
        public int Changelist = 0;

        [JsonPropertyName("GameName")]
        public string GameName = string.Empty;

        [JsonPropertyName("ProcessName")]
        public string ProcessName = string.Empty;

        [JsonPropertyName("SessionId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid SessionId = Guid.Empty;
    }
}
