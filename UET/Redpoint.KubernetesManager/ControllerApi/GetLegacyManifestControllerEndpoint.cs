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
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IPathProvider _pathProvider;

        public GetLegacyManifestControllerEndpoint(
            ICertificateManager certificateManager,
            IKubeConfigManager kubeConfigManager,
            IPathProvider pathProvider)
        {
            _certificateManager = certificateManager;
            _kubeConfigManager = kubeConfigManager;
            _pathProvider = pathProvider;
        }

        public string Path => "/manifest";

        public async Task HandleAsync(HttpListenerContext context)
        {
            var remoteAddress = context.Request.RemoteEndPoint.Address;
            var nodeName = context.Request.QueryString.Get("nodeName");

            var certificateAuthority = await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath("ca", "ca"));

            var nodeCertificate = await _certificateManager.EnsureGeneratedForNodeAsync(nodeName!, remoteAddress);
            var nodeKubeletConfig = await _kubeConfigManager.EnsureGeneratedForNodeAsync(certificateAuthority, nodeName!);

            var nodeManifest = new NodeManifest
            {
                ServerRKMInstallationId = _pathProvider.RKMInstallationId,
                NodeName = nodeName!,
                CertificateAuthority = certificateAuthority,
                NodeCertificate = nodeCertificate.CertificatePem,
                NodeCertificateKey = nodeCertificate.PrivateKeyPem,
                NodeKubeletConfig = nodeKubeletConfig,
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
