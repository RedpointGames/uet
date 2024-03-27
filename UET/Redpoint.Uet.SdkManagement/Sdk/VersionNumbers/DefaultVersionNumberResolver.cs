namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Linq;

    internal class DefaultVersionNumberResolver : IVersionNumberResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultVersionNumberResolver(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public T For<T>(string unrealEnginePath) where T : IVersionNumbers
        {
            var services = _serviceProvider.GetServices<T>();
            var preferredService = services
                .Where(x => x.CanUse(unrealEnginePath))
                .OrderByDescending(x => x.Priority)
                .FirstOrDefault();
            if (preferredService == null)
            {
                throw new NotSupportedException("This Unreal Engine does not provide SDK version numbers in a format that UET can understand.");
            }
            return preferredService;
        }
    }
}
