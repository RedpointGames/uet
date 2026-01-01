namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal class AuthorizeNodeRequest
    {
        [JsonPropertyName("capablePlatforms")]
        public required IList<RkmNodePlatform> CapablePlatforms { get; set; }

        [JsonPropertyName("architecture")]
        public required string Architecture { get; set; }
    }
}
