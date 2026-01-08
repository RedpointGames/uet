namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultFileTransferServer : IFileTransferServer
    {
        private readonly ILogger<DefaultFileTransferServer> _logger;

        public DefaultFileTransferServer(
            ILogger<DefaultFileTransferServer> logger)
        {
            _logger = logger;
        }

        public async Task HandleDownloadFileAsync(
            HttpContext httpContext,
            Stream contentStream,
            bool leaveOpen)
        {
            try
            {
                MemoryStream? streamToDispose = null;
                long contentLength = -1;
                if (contentStream is FileStream fileStream)
                {
                    contentLength = new FileInfo(fileStream.Name).Length;
                }
                if (contentLength == -1)
                {
                    try
                    {
                        contentLength = contentStream.Length;
                    }
                    catch
                    {
                    }
                }
                if (!contentStream.CanSeek || contentLength == -1)
                {
                    streamToDispose = new MemoryStream();
                }
                try
                {
                    if (streamToDispose != null)
                    {
                        await contentStream.CopyToAsync(
                            streamToDispose,
                            httpContext.RequestAborted);
                        streamToDispose.Seek(0, SeekOrigin.Begin);
                        contentStream = streamToDispose;
                        contentLength = contentStream.Length;
                    }

                    httpContext.Response.StatusCode = 200;
                    httpContext.Response.Headers.Add(
                        "Content-Type",
                        "application/octet-stream");
                    httpContext.Response.Headers.Add(
                        "Content-Length",
                        contentLength.ToString(CultureInfo.InvariantCulture));

                    var hash = Hash.Sha256AsHexString(contentStream);
                    contentStream.Seek(0, SeekOrigin.Begin);
                    httpContext.Response.Headers.Add("Content-Hash", $"sha256:{hash}");

                    if (!HttpMethods.IsHead(httpContext.Request.Method))
                    {
                        _logger.LogInformation($"Sending data for {httpContext.Request.Path} ({contentLength} bytes)...");
                        await contentStream.CopyToAsync(
                            httpContext.Response.Body,
                            httpContext.RequestAborted);
                    }
                }
                finally
                {
                    if (streamToDispose != null)
                    {
                        streamToDispose.Dispose();
                    }
                }
            }
            finally
            {
                if (!leaveOpen)
                {
                    contentStream.Dispose();
                }
            }
        }

        public async Task HandleUploadFileAsync(
            HttpContext httpContext,
            string targetPath)
        {
            if (!httpContext.Request.Headers.TryGetValue("Intent", out var intent) ||
                intent.Count != 1 ||
                intent != "upload")
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (HttpMethods.IsHead(httpContext.Request.Method))
            {
                var existingHash = string.Empty;
                if (File.Exists(targetPath))
                {
                    using (var fileStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        existingHash = $"sha256:{Hash.Sha256AsHexString(fileStream)}";
                    }
                }

                if (!httpContext.Response.Headers.TryGetValue("Content-Hash", out var clientHash) ||
                    clientHash.Count != 1 ||
                    string.IsNullOrWhiteSpace(clientHash[0]) ||
                    clientHash[0] != existingHash)
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                else
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                }
                return;
            }

            var targetDirectoryPath = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectoryPath))
            {
                Directory.CreateDirectory(targetDirectoryPath);
            }
            using (var fileStream = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await httpContext.Request.Body.CopyToAsync(
                    fileStream,
                    httpContext.RequestAborted);
            }

            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
        }
    }
}
