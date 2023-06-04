namespace Redpoint.UET.Configuration.Dynamic
{
    internal class BuildConfigDeploymentConverter<TDistribution> : BuildConfigDynamicConverter<TDistribution, IDeploymentProvider>
    {
        public BuildConfigDeploymentConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "deployment";
    }
}
