namespace Redpoint.UET.Configuration.Project
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectIncludeFragment
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions"), JsonRequired]
        public List<BuildConfigProjectDistribution> Distributions { get; set; } = new List<BuildConfigProjectDistribution>();
    }
}
