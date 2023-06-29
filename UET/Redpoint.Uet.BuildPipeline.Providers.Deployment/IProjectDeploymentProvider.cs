namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;

    public interface IProjectDeploymentProvider : IDeploymentProvider, IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>
    {
    }
}
