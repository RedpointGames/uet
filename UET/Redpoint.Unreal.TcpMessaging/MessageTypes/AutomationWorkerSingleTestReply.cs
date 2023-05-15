namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerSingleTestReply")]
    public record class AutomationWorkerSingleTestReply
    {
        [JsonPropertyName("DisplayName")]
        public string DisplayName = string.Empty;

        [JsonPropertyName("FullTestPath")]
        public string FullTestPath = string.Empty;

        [JsonPropertyName("TestName")]
        public string TestName = string.Empty;

        [JsonPropertyName("TestParameter")]
        public string TestParameter = string.Empty;

        [JsonPropertyName("SourceFile")]
        public string SourceFile = string.Empty;

        [JsonPropertyName("SourceFileLine")]
        public int SourceFileLine = 0;

        [JsonPropertyName("AssetPath")]
        public string AssetPath = string.Empty;

        [JsonPropertyName("OpenCommand")]
        public string OpenCommand = string.Empty;

        [JsonPropertyName("TestFlags")]
        public uint TestFlags = 0;

        [JsonPropertyName("NumParticipantsRequired")]
        public uint NumParticipantsRequired = 0;
    }
}
