namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerRunTestsReply")]
    public record class AutomationWorkerRunTestsReply
    {
        [JsonPropertyName("TestName")]
        public string TestName = string.Empty;

        [JsonPropertyName("Entries")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This value is used in JSON serialization.")]
        public List<AutomationExecutionEntry> Entries = new List<AutomationExecutionEntry>();

        [JsonPropertyName("WarningTotal")]
        public int WarningTotal = 0;

        [JsonPropertyName("ErrorTotal")]
        public int ErrorTotal = 0;

        [JsonPropertyName("Duration")]
        public float Duration = 0;

        [JsonPropertyName("ExecutionCount")]
        public uint ExecutionCount = 0;

        [JsonPropertyName("State")]
        public string State = AutomationState.NotRun;
    }
}
