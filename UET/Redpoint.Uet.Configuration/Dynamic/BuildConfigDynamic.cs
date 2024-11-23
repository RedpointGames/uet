namespace Redpoint.Uet.Configuration.Dynamic
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A dynamically driven preparation, test or deployment step.
    /// </summary>
    /// <typeparam name="TDistribution">The distribution type, which specifies whether this is for projects or plugins.</typeparam>
    /// <typeparam name="TBaseClass">The base class for configuration, which specifies whether this is for preparation, tests or deployment.</typeparam>
    public class BuildConfigDynamic<TDistribution, TBaseClass>
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all tests defined.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the test/deployment type.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// If set, this will be emitted as a manual job on build servers. Only applies to deployments. Defaults to false.
        /// </summary>
        [JsonPropertyName("Manual")]
        public bool? Manual { get; set; }

        /// <summary>
        /// The dynamic settings associate with this test/deployment type. To be consumed by the dynamic test/deployment provider based on the type.
        /// </summary>
        public object DynamicSettings { get; set; } = new object();
    }
}
