namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class AuthorizeNodeRequest
    {
        [JsonPropertyName("capablePlatforms")]
        public required IList<RkmNodePlatform> CapablePlatforms { get; set; }

        [JsonPropertyName("architecture")]
        public required string Architecture { get; set; }
    }
}
