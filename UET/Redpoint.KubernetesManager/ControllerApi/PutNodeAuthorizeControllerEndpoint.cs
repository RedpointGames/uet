namespace Redpoint.KubernetesManager.ControllerApi
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Services;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PutNodeAuthorizeControllerEndpoint : IControllerEndpoint
    {
        private readonly ITpmService _tpmService;
        private readonly ICertificateManager _certificateManager;
        private readonly IPathProvider _pathProvider;

        public PutNodeAuthorizeControllerEndpoint(
            ITpmService tpmService,
            ICertificateManager certificateManager,
            IPathProvider pathProvider)
        {
            _tpmService = tpmService;
            _certificateManager = certificateManager;
            _pathProvider = pathProvider;
        }

        public string Path => "/node-authorize";

        public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // This should be a PUT request.
            if (context.Request.Method != "PUT")
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            // Deserialize the request.
            var nodeAuthorizeRequest = JsonSerializer.Deserialize(
                context.Request.Body,
                ManifestJsonSerializerContext.Default.NodeAuthorizeRequest)!;
            var ekPublicKeyTpmRepresentation = Convert.FromBase64String(nodeAuthorizeRequest.EkPublicTpmRepresentationBase64);
            var aikPublicKeyTpmRepresentation = Convert.FromBase64String(nodeAuthorizeRequest.AikPublicTpmRepresentationBase64);

            // @todo: Authorize either:
            // - If the EK and AIK match our own, then this request is coming from the controller itself. We must authorize here since the API server won't necessarily be running at this point.
            // - If they don't match our own, check RkmNode resources on the API server.
            var authorized = true;
            var nodeName = nodeAuthorizeRequest.SuggestedNodeName;

            // If we are not authorized, return appropriate HTTP response.
            if (!authorized)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            // Generate node certificate.
            var nodeCertificate = await _certificateManager.GenerateCertificateForAuthorizedNodeAsync(
                nodeName,
                context.Connection.RemoteIpAddress);
            var certificateAuthorityPem = await File.ReadAllTextAsync(
                System.IO.Path.Combine(_pathProvider.RKMRoot, "certs", "ca", "ca.pem"),
                cancellationToken);
            var bundle = new NodeAuthorizeResponseEncryptedBundle
            {
                NodePrivateKeyPem = nodeCertificate.PrivateKeyPem,
                NodeCertificatePem = nodeCertificate.CertificatePem,
                CertificateAuthorityPem = certificateAuthorityPem,
                NodeName = nodeName,
            };

            // Authorize and encrypt bundle.
            var (envelopingKey, encryptedBundle) = _tpmService.Authorize(
                ekPublicKeyTpmRepresentation,
                aikPublicKeyTpmRepresentation,
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    bundle,
                    ManifestJsonSerializerContext.Default.NodeAuthorizeResponseEncryptedBundle)));

            // Send response payload.
            var response = new NodeAuthorizeResponse
            {
                EnvelopingKeyBase64 = Convert.ToBase64String(envelopingKey),
                EncryptedBundleBase64 = Convert.ToBase64String(encryptedBundle),
            };
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                ManifestJsonSerializerContext.Default.NodeAuthorizeResponse,
                cancellationToken);
        }
    }
}
