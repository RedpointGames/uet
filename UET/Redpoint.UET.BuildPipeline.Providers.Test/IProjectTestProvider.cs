namespace Redpoint.UET.BuildPipeline.Providers.Test
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Project;

    public interface IProjectTestProvider :
        ITestProvider,
        IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>
    {
    }
}