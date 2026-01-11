namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    internal class IpxeUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        private readonly ILogger<IpxeUnauthenticatedFileTransferEndpoint> _logger;

        public IpxeUnauthenticatedFileTransferEndpoint(
            ILogger<IpxeUnauthenticatedFileTransferEndpoint> logger)
        {
            _logger = logger;
        }

        public string[] Prefixes => ["/ipxe.efi"];

        public async Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (request.PathRemaining.HasValue)
            {
                // Only matches "/ipxe.efi" exactly.
                return null;
            }

            var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                request.RemoteAddress.ToString(),
                CancellationToken.None);

            return new FileStream(
                Path.Combine(request.StaticFilesDirectory.FullName, "ipxe.efi"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
    }
}
