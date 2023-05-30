namespace Redpoint.UET.Configuration.Project
{
    using Redpoint.UET.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BuildConfigProject : BuildConfig
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions"), JsonRequired]
        public BuildConfigProjectDistribution[] Distributions { get; set; } = new BuildConfigProjectDistribution[0];
    }
}
