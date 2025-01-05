namespace Redpoint.CloudFramework.Startup
{
    public class BoundHelmConfiguration : IOptionalHelmConfiguration
    {
        private readonly HelmConfiguration _config;

        public BoundHelmConfiguration(HelmConfiguration config)
        {
            _config = config;
        }

        public HelmConfiguration GetHelmConfig()
        {
            return _config;
        }
    }
}
