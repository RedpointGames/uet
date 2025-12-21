namespace Redpoint.KubernetesManager.ControllerApi
{
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class GetLegacyManifestControllerEndpoint : IControllerEndpoint
    {
        private readonly ICertificateManager _certificateManager;
        private readonly IPathProvider _pathProvider;

        public GetLegacyManifestControllerEndpoint(
            ICertificateManager certificateManager,
            IPathProvider pathProvider)
        {
            _certificateManager = certificateManager;
            _pathProvider = pathProvider;
        }

        public string Path => "/manifest";

        public async Task HandleAsync(HttpListenerContext context)
        {
            var remoteAddress = context.Request.RemoteEndPoint.Address;
            var nodeName = context.Request.QueryString.Get("nodeName");

            var certificateAuthority = await File.ReadAllTextAsync(System.IO.Path.Combine(_pathProvider.RKMRoot, "certs", "ca", "ca.pem"));

            var nodeCertificate = await _certificateManager.GenerateCertificateForAuthorizedNodeAsync(nodeName!, remoteAddress);

            var nodeManifest = new NodeManifest
            {
                ServerRKMInstallationId = _pathProvider.RKMInstallationId,
                NodeName = nodeName!,
                CertificateAuthority = certificateAuthority,
                NodeCertificate = nodeCertificate.CertificatePem,
                NodeCertificateKey = nodeCertificate.PrivateKeyPem,
            };

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.AddHeader("Content-Type", "text/yaml");
            using (var writer = new StreamWriter(context.Response.OutputStream, leaveOpen: true))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(
                    nodeManifest,
                    KubernetesJsonSerializerContext.Default.NodeManifest));
            }
        }
    }
}
