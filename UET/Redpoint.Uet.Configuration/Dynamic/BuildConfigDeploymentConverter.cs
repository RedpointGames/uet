namespace Redpoint.Uet.Configuration.Dynamic
{
    internal sealed class BuildConfigDeploymentConverter<TDistribution> : BuildConfigDynamicConverter<TDistribution, IDeploymentProvider>
    {
        public BuildConfigDeploymentConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "deployment";
    }
}
