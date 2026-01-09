namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeGroupSpecServices
    {
        [JsonPropertyName("keepAlive")]
        public IList<string?>? KeepAlive { get; set; }
    }
}
