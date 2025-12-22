namespace Redpoint.KubernetesManager.Manifest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    /// <summary>
    /// Used by the node to send it's public EK and AIK from the TPM to the controller.
    /// </summary>
    public class NodeAuthorizeRequest
    {
        [JsonPropertyName("ekPublicTpmRepresentationBase64")]
        public required string EkPublicTpmRepresentationBase64 { get; set; }

        [JsonPropertyName("aikPublicTpmRepresentationBase64")]
        public required string AikPublicTpmRepresentationBase64 { get; set; }

        [JsonPropertyName("suggestedNodeName")]
        public required string SuggestedNodeName { get; set; }
    }
}
