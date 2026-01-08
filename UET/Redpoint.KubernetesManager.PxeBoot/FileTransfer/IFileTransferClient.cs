namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IFileTransferClient
    {
        Task DownloadFilesAsync(
            Dictionary<Uri, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default);

        Task DownloadFilesAsync(
            Uri sourceBaseUri,
            Dictionary<string, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default);

        Task DownloadFilesAsync(
            Uri sourceBaseUri,
            string targetBasePath,
            Dictionary<string, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default);

        Task DownloadFileAsync(
            Uri source,
            string target,
            HttpClient? client = null,
            CancellationToken cancellationToken = default);

        Task UploadFileAsync(
            string source,
            Uri target,
            HttpClient client,
            CancellationToken cancellationToken = default);
    }
}
