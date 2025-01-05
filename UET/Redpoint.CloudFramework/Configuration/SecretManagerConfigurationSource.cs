namespace Redpoint.CloudFramework.Configuration
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    internal class SecretManagerConfigurationSource : IConfigurationSource
    {
        private readonly IServiceProvider _serviceProvider;

        public SecretManagerConfigurationSource(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return _serviceProvider.GetRequiredService<SecretManagerConfigurationProvider>();
        }
    }
}
