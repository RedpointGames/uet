namespace Redpoint.Uet.Configuration.Project
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class BuildConfigProject : BuildConfig
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions")]
        public List<BuildConfigProjectDistribution> Distributions { get; set; } = new List<BuildConfigProjectDistribution>();
    }
}
