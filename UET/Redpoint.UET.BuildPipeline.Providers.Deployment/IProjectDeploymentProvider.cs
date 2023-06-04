namespace Redpoint.UET.BuildPipeline.Providers.Deployment
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Project;

    public interface IProjectDeploymentProvider : IDeploymentProvider, IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>
    {
    }
}
