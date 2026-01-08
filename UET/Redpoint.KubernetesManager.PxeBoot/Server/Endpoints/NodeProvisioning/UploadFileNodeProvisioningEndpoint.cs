namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.Hashing;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class UploadFileNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        private readonly IFileTransferServer _fileTransferServer;

        public UploadFileNodeProvisioningEndpoint(
            IFileTransferServer fileTransferServer)
        {
            _fileTransferServer = fileTransferServer;
        }

        public string Path => "/upload-file";

        public bool RequireNodeObjects => true;

        public async Task HandleRequestAsync(
            INodeProvisioningEndpointContext context)
        {
            if (!context.HttpContext.Request.Query.TryGetValue("name", out var names) ||
                names.Count != 1 ||
                string.IsNullOrWhiteSpace(names[0]))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            await _fileTransferServer.HandleUploadFileAsync(
                context.HttpContext,
                System.IO.Path.Combine(
                    context.NodeFileStorageDirectory.FullName,
                    Hash.Sha256AsHexString(names[0]!, Encoding.UTF8)));
        }
    }
}
