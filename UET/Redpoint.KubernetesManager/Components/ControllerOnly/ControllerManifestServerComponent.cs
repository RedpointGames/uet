namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.ControllerApi;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Kestrel;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This component serves the controller API endpoints, including the manifest endpoint used by <see cref="NodeManifestServerComponent" />.
    /// </summary>
    internal class ControllerManifestServerComponent : AbstractHttpListenerComponent
    {
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IEnumerable<IControllerEndpoint> _endpoints;

        public ControllerManifestServerComponent(
            ILogger<ControllerManifestServerComponent> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            ILocalEthernetInfo localEthernetInfo,
            IEnumerable<IControllerEndpoint> endpoints,
            IKestrelFactory kestrelFactory,
            ICertificateManager? certificateManager = null) : base(
                logger,
                hostApplicationLifetime,
                kestrelFactory,
                certificateManager)
        {
            _localEthernetInfo = localEthernetInfo;
            _endpoints = endpoints;
        }

        protected override string ServerDescription => "controller API and manifest server";

        protected override IPAddress ListeningAddress => _localEthernetInfo.IPAddress;

        protected override int ListeningPort => 8374;

        protected override int? SecureListeningPort => 8376; // 8375 is already used by local node manifest server.

        protected override bool IsControllerOnly => true;

        protected override async Task HandleIncomingRequestAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var handled = false;
            foreach (var endpoint in _endpoints)
            {
                if (context.Request.Path == endpoint.Path)
                {
                    handled = true;
                    await endpoint.HandleAsync(context, cancellationToken);
                    break;
                }
            }
            if (!handled)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }

        protected override async Task OnStartingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for everything to be available to serve requests.
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeconfigsReady);
        }
    }
}
