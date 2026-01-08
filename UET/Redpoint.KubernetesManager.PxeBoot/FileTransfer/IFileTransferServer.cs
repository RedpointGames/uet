namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using Microsoft.AspNetCore.Http;
    using System.IO;
    using System.Threading.Tasks;

    internal interface IFileTransferServer
    {
        Task HandleDownloadFileAsync(
            HttpContext httpContext,
            Stream contentStream,
            bool leaveOpen = false);

        Task HandleUploadFileAsync(
            HttpContext httpContext,
            string targetPath);
    }
}
