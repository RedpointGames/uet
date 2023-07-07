namespace Redpoint.Uet.Configuration.Dynamic
{
    internal class BuildConfigPrepareConverter<TDistribution> : BuildConfigDynamicConverter<TDistribution, IPrepareProvider>
    {
        public BuildConfigPrepareConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "prepare";
    }
}
