namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;

    public interface IProjectTestProvider :
        ITestProvider,
        IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>
    {
    }
}