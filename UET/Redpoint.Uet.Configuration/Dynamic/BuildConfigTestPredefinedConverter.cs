namespace Redpoint.Uet.Configuration.Dynamic
{
    using Redpoint.Uet.Configuration.Plugin;

    internal sealed class BuildConfigTestPredefinedConverter<TDistribution> : BuildConfigPredefinedDynamicConverter<TDistribution, ITestProvider, BuildConfigPluginPredefinedTestDependencies>
    {
        public BuildConfigTestPredefinedConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "test";
    }
}
