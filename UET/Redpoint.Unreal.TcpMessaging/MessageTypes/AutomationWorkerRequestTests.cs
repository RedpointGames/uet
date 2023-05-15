namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerRequestTests")]
    public record class AutomationWorkerRequestTests
    {
        [JsonPropertyName("DeveloperDirectoryIncluded")]
        public bool DeveloperDirectoryIncluded = false;

        [JsonPropertyName("RequestedTestFlags")]
        public uint RequestedTestFlags =
            AutomationTestFlags.EditorContext |
            AutomationTestFlags.ProductFilter;
    }
}
