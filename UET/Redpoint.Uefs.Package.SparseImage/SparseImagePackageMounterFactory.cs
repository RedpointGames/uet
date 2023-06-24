using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Redpoint.Uefs.Package.SparseImage
{
    [SupportedOSPlatform("macos")]
    internal class SparseImagePackageMounterFactory : IPackageMounterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SparseImagePackageMounterFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Memory<byte> MagicHeader => SparseImagePackageMounter.MagicHeader;

        public IPackageMounter CreatePackageMounter()
        {
            return new SparseImagePackageMounter(
                _serviceProvider.GetRequiredService<ILogger<SparseImagePackageMounter>>());
        }
    }
}
