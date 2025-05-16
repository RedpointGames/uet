namespace Redpoint.KubernetesManager.Models.Hcs
{
    using System.Text.Json.Serialization;

    internal class HcsComputeSystemWithId : HcsComputeSystem
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;
    }
}
