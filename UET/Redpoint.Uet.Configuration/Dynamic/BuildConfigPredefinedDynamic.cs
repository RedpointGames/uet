namespace Redpoint.Uet.Configuration.Dynamic
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A dynamically driven preparation, test or deployment step that is defined outside of a distribution, where dependencies need to be specified.
    /// </summary>
    /// <typeparam name="TDistribution">The distribution type, which specifies whether this is for projects or plugins.</typeparam>
    /// <typeparam name="TBaseClass">The base class for configuration, which specifies whether this is for preparation, tests or deployment.</typeparam>
    /// <typeparam name="TDependencies">The class that describes the dependencies of this item when not referenced as part of a distribution.</typeparam>
    public class BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies> : BuildConfigDynamic<TDistribution, TBaseClass>
    {
        /// <summary>
        /// For tests defined outside of distributions, an optional shorter name that can be used with `uet test`.
        /// </summary>
        [JsonPropertyName("ShortName")]
        public string? ShortName { get; set; }

        /// <summary>
        /// If set, the dependencies that will be processed/required before this entry can be used.
        /// </summary>
        [JsonPropertyName("Dependencies")]
        public TDependencies? Dependencies { get; set; }
    }
}
