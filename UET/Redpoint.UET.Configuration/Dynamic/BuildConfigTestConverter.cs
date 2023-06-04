namespace Redpoint.UET.Configuration.Dynamic
{
    internal class BuildConfigTestConverter<TDistribution> : BuildConfigDynamicConverter<TDistribution, ITestProvider>
    {
        public BuildConfigTestConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "test";
    }
}
