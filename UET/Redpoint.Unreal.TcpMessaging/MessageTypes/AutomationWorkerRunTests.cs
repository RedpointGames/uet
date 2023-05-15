namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerRunTests")]
    public record class AutomationWorkerRunTests
    {
        [JsonPropertyName("ExecutionCount")]
        public uint ExecutionCount = 1;

        [JsonPropertyName("RoleIndex")]
        public int RoleIndex = 0;

        [JsonPropertyName("TestName")]
        public string TestName = string.Empty;

        [JsonPropertyName("BeautifiedTestName")]
        public string BeautifiedTestName = string.Empty;

        [JsonPropertyName("FullTestPath")]
        public string FullTestPath = string.Empty;

        [JsonPropertyName("bSendAnalytics")]
        public bool bSendAnalytics = true;
    }
}
