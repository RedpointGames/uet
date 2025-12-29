namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using System.Text.Json.Serialization;

    internal class PartedOutput
    {
        [JsonPropertyName("disk")]
        public PartedDisk? Disk { get; set; }
    }
}
