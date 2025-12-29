namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeStatusProvisioner
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        [JsonPropertyName("lastStepCommittedIndex")]
        public int? LastStepCommittedIndex { get; set; }

        [JsonPropertyName("rebootStepIndex")]
        public int? RebootStepIndex { get; set; }

        [JsonPropertyName("rebootNotificationForOnceViaNotifyOccurred")]
        public bool? RebootNotificationForOnceViaNotifyOccurred { get; set; }

        [JsonPropertyName("currentStepIndex")]
        public int? CurrentStepIndex { get; set; }

        [JsonPropertyName("currentStepStarted")]
        public bool? CurrentStepStarted { get; set; }
    }
}
