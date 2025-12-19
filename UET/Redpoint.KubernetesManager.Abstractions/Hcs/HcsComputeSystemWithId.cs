namespace Redpoint.KubernetesManager.Abstractions.Hcs
{
    using System.Text.Json.Serialization;

    public class HcsComputeSystemWithId : HcsComputeSystem
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;
    }
}
