namespace Redpoint.Uefs.Package.SparseImage
{
    using System.Runtime.Versioning;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;

    [SupportedOSPlatform("macos")]
    internal sealed class SparseImagePackageMounterFactory : IPackageMounterFactory
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly ILogger<SparseImagePackageMounter> _logger;

        public SparseImagePackageMounterFactory(
            IProcessExecutor processExecutor,
            IPathResolver pathResolver,
            ILogger<SparseImagePackageMounter> logger)
        {
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
            _logger = logger;
        }

        public Memory<byte> MagicHeader => SparseImagePackageMounter.MagicHeader;

        public IPackageMounter CreatePackageMounter()
        {
            return new SparseImagePackageMounter(
                _processExecutor,
                _pathResolver,
                _logger);
        }
    }
}
