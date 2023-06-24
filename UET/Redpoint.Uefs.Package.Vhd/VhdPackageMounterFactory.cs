namespace Redpoint.Uefs.Package.Vhd
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class VhdPackageMounterFactory : IPackageMounterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public VhdPackageMounterFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Memory<byte> MagicHeader => VhdPackageMounter.MagicHeader;

        public IPackageMounter CreatePackageMounter()
        {
            return new VhdPackageMounter(
                _serviceProvider.GetRequiredService<ILogger<VhdPackageMounter>>());
        }
    }
}
