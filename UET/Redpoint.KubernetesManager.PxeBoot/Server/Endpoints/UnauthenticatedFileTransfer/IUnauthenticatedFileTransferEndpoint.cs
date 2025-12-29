namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Redpoint.KubernetesManager.Configuration.Sources;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal interface IUnauthenticatedFileTransferEndpoint
    {
        string[] Prefixes { get; }

        Task<Stream?> GetDownloadStreamAsync(
            UnauthenticatedFileTransferRequest request,
            CancellationToken cancellationToken);
    }
}
