namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class RkmProvisionContextUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        public string[] Prefixes => ["/rkm-provision-context.json"];

        public async Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (request.HttpContext == null)
            {
                // TFTP not supported.
                return null;
            }

            var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                request.RemoteAddress.ToString(),
                cancellationToken);

            var bootedFromStepIndex = node?.Status?.Provisioner?.RebootStepIndex ?? -1;

            var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(
                stream,
                new WindowsRkmProvisionContext
                {
                    ApiAddress = request.HttpContext.Connection.LocalIpAddress.ToString(),
                    BootedFromStepIndex = bootedFromStepIndex,
                    IsInRecovery = false,
                },
                WindowsRkmProvisionJsonSerializerContext.Default.WindowsRkmProvisionContext,
                cancellationToken);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}
