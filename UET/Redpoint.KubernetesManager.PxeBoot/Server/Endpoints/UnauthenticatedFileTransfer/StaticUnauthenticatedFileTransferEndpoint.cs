namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    internal class StaticUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        public StaticUnauthenticatedFileTransferEndpoint(
            ILogger<StaticUnauthenticatedFileTransferEndpoint> logger)
        {
            _logger = logger;
        }

        public string[] Prefixes => ["/static"];

        private static readonly string[] _allowedFilenames =
            [
                // @note: ipxe.efi is served as both /ipxe.efi and /static/ipxe.efi, but
                // only /ipxe.efi denies serving the file if the machine should boot from
                // disk. This ensures that the "recovery setup" can always download ipxe.efi
                // via the static endpoint.
                "ipxe.efi",
                "wimboot",
                "background.png",
                "vmlinuz",
                "initrd",
                "uet",
                "uet.exe",
            ];
        private readonly ILogger<StaticUnauthenticatedFileTransferEndpoint> _logger;

        public Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (!request.PathRemaining.HasValue)
            {
                // No filename specified.
                _logger.LogWarning("/static endpoint had no path specified.");
                return Task.FromResult<Stream?>(null);
            }

            var targetName = request.PathRemaining.Value.TrimStart('/');

            if (!_allowedFilenames.Contains(targetName))
            {
                // Not a permitted filename.
                _logger.LogWarning($"/static endpoint received filename '{targetName}', but this is not a permitted filename.");
                return Task.FromResult<Stream?>(null);
            }

            var targetPath = Path.Combine(request.StaticFilesDirectory.FullName, targetName);
            if (!File.Exists(targetPath))
            {
                // File does not exist.
                _logger.LogWarning($"/static endpoint received filename '{targetName}', but this file does not exist on disk.");
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read));
        }
    }
}
