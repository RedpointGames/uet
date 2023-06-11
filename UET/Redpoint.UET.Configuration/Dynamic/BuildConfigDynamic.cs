namespace Redpoint.UET.Configuration.Dynamic
{
    public class BuildConfigDynamic<TDistribution, TBaseClass>
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all tests defined.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the test/deployment type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// If set, this will be emitted as a manual job on build servers. Only applies to deployments. Defaults to false.
        /// </summary>
        public bool? Manual { get; set; }

        /// <summary>
        /// The dynamic settings associate with this test/deployment type. To be consumed by the dynamic test/deployment provider based on the type.
        /// </summary>
        public object DynamicSettings { get; set; } = new object();
    }
}
