namespace Redpoint.Uet.Configuration.Dynamic
{
    public class BuildConfigPrepareConverter<TDistribution> : BuildConfigDynamicConverter<TDistribution, IPrepareProvider>
    {
        public BuildConfigPrepareConverter(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override string Noun => "prepare";
    }
}
