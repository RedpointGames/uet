namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerRequestTestsReplyComplete")]
    public record class AutomationWorkerRequestTestsReplyComplete
    {
        [JsonPropertyName("Tests")]
        public List<AutomationWorkerSingleTestReply> Tests = new List<AutomationWorkerSingleTestReply>();
    }
}
