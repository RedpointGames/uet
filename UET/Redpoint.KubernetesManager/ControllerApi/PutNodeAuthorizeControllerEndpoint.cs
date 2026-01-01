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
        private readonly ICertificateManager _certificateManager;
        private readonly IPathProvider _pathProvider;

        public PutNodeAuthorizeControllerEndpoint(
            ICertificateManager certificateManager,
            IPathProvider pathProvider)
        {
            _certificateManager = certificateManager;
            _pathProvider = pathProvider;
        }

        public string Path => "/node-authorize";

        public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // @todo: This needs to use the new TPM-secured HTTP server interface.
            throw new NotImplementedException();
        }
    }
}
