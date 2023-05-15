namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    using Redpoint.Unreal.Serialization;
    using System.Text.Json.Serialization;

    [TopLevelAssetPath("/Script/AutomationMessages", "AutomationWorkerFindWorkersResponse")]
    public record class AutomationWorkerFindWorkersResponse
    {
        [JsonPropertyName("DeviceName")]
        public string DeviceName = string.Empty;

        [JsonPropertyName("InstanceName")]
        public string InstanceName = string.Empty;

        [JsonPropertyName("Platform")]
        public string Platform = string.Empty;

        [JsonPropertyName("OSVersionName")]
        public string OSVersionName = string.Empty;

        [JsonPropertyName("ModelName")]
        public string ModelName = string.Empty;

        [JsonPropertyName("GPUName")]
        public string GPUName = string.Empty;

        [JsonPropertyName("CPUModelName")]
        public string CPUModelName = string.Empty;

        [JsonPropertyName("RAMInGB")]
        public uint RAMInGB = 0;

        [JsonPropertyName("RenderModeName")]
        public string RenderModeName = string.Empty;

        [JsonPropertyName("SessionId"), JsonConverter(typeof(UnrealGuidConverter))]
        public Guid SessionId = Guid.Empty;

        [JsonPropertyName("RHIName")]
        public string RHIName = string.Empty;
    }
}
