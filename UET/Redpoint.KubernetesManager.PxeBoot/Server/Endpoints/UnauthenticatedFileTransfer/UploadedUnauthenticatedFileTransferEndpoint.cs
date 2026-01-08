namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Redpoint.Hashing;
    using System.Text;
    using System.Threading.Tasks;

    internal class UploadedUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        public string[] Prefixes => ["/uploaded"];

        public async Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (!request.PathRemaining.HasValue)
            {
                // No filename specified.
                return null;
            }

            var targetName = request.PathRemaining.Value.TrimStart('/');

            var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                request.RemoteAddress.ToString(),
                cancellationToken);
            if (node == null ||
                string.IsNullOrWhiteSpace(node?.Status?.AttestationIdentityKeyFingerprint))
            {
                // No node recognised by this IP address, so can't serve any uploaded files.
                return null;
            }

            var targetPath = Path.Combine(
                request.StorageFilesDirectory.FullName,
                node.Status.AttestationIdentityKeyFingerprint,
                Hash.Sha256AsHexString(targetName, Encoding.UTF8));
            if (!File.Exists(targetPath))
            {
                // File does not exist.
                return null;
            }

            return new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
    }
}
