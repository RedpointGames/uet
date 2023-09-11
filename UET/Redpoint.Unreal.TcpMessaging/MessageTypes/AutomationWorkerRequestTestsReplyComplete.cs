namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerRequestTestsReplyComplete")]
    public record class AutomationWorkerRequestTestsReplyComplete
    {
        [JsonPropertyName("Tests")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This value is used in JSON serialization.")]
        public List<AutomationWorkerSingleTestReply> Tests = new List<AutomationWorkerSingleTestReply>();
    }
}
