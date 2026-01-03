namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class RkmNodeProvisionerSpec
    {
        [JsonPropertyName("steps")]
        public IList<RkmNodeProvisionerStep?>? Steps { get; set; }
    }
}
