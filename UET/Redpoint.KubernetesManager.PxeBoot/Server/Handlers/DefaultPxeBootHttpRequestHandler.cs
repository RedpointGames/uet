namespace Redpoint.KubernetesManager.PxeBoot.Server.Handlers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultPxeBootHttpRequestHandler : IPxeBootHttpRequestHandler
    {
        private readonly ILogger<DefaultPxeBootHttpRequestHandler> _logger;
        private readonly IFileTransferServer _fileTransferServer;
        private readonly List<IUnauthenticatedFileTransferEndpoint> _unauthenticatedFileTransferEndpoints;
        private readonly List<INodeProvisioningEndpoint> _nodeProvisioningEndpoints;

        public DefaultPxeBootHttpRequestHandler(
            ILogger<DefaultPxeBootHttpRequestHandler> logger,
            IFileTransferServer fileTransferServer,
            IEnumerable<IUnauthenticatedFileTransferEndpoint> unauthenticatedFileTransferEndpoints,
            IEnumerable<INodeProvisioningEndpoint> nodeProvisioningEndpoints)
        {
            _logger = logger;
            _fileTransferServer = fileTransferServer;
            _unauthenticatedFileTransferEndpoints = unauthenticatedFileTransferEndpoints.ToList();
            _nodeProvisioningEndpoints = nodeProvisioningEndpoints.ToList();
        }

        public async Task<bool> TryHandleAsUnauthenticatedFileTransfer(
            PxeBootServerContext serverContext,
            HttpContext httpContext)
        {
            foreach (var endpoint in _unauthenticatedFileTransferEndpoints)
            {
                foreach (var prefix in endpoint.Prefixes)
                {
                    _logger.LogTrace($"HTTP: Checking request path '{httpContext.Request.Path}' against '{prefix}'...");
                    if (httpContext.Request.Path.StartsWithSegments(prefix, out var remaining))
                    {
                        _logger.LogTrace($"HTTP: Matched path against prefix, with remaining '{remaining}'.");
                        var request = new UnauthenticatedFileTransferRequest
                        {
                            PathPrefix = prefix,
                            PathRemaining = remaining,
                            RemoteAddress = httpContext.Connection.RemoteIpAddress,
                            IsTftp = false,
                            ConfigurationSource = serverContext.ConfigurationSource,
                            StaticFilesDirectory = serverContext.StaticFilesDirectory,
                            StorageFilesDirectory = serverContext.StorageFilesDirectory,
                            HostHttpPort = serverContext.HostHttpPort,
                            HostHttpsPort = serverContext.HostHttpsPort,
                            JsonSerializerContext = serverContext.JsonSerializerContext,
                            HttpContext = httpContext,
                        };
                        try
                        {
                            var stream = await endpoint.GetDownloadStreamAsync(
                                request,
                                CancellationToken.None);
                            if (stream != null)
                            {
                                _logger.LogInformation($"HTTP: Successfully returning stream for '{httpContext.Request.Path}'.");
                                await _fileTransferServer.HandleDownloadFileAsync(
                                    httpContext,
                                    stream);
                                return true;
                            }
                        }
                        catch (DenyUnauthenticatedFileTransferException)
                        {
                            _logger.LogInformation($"HTTP: Explicitly denied access to path '{httpContext.Request.Path}'.");
                            httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private async Task<bool> TryHandleAsNodeProvisioning(PxeBootServerContext serverContext, HttpContext httpContext)
        {
            if (!httpContext.Request.Path.StartsWithSegments("/api/node-provisioning", out var remaining))
            {
                // Not a node provisioning endpoint.
                return false;
            }

            var tpmSecuredHttpServer = serverContext.GetTpmSecuredHttpServer();

            if (remaining == "/negotiate-certificate")
            {
                // Handle negotiation.
                await tpmSecuredHttpServer.HandleNegotiationRequestAsync(httpContext);
                return true;
            }

            var pem = await tpmSecuredHttpServer.GetAikPemVerifiedByClientCertificateAsync(httpContext);
            var fingerprint = RkmNodeFingerprint.CreateFromPem(pem);

            var nodeProvisioningEndpoint = _nodeProvisioningEndpoints.FirstOrDefault(x => x.Path == remaining);
            if (nodeProvisioningEndpoint == null)
            {
                // Not a node provisioning endpoint.
                return false;
            }

            var endpointContext = new DefaultNodeProvisioningEndpointContext(
                _logger,
                httpContext,
                pem,
                fingerprint,
                serverContext.ConfigurationSource,
                serverContext.JsonSerializerContext,
                serverContext.StorageFilesDirectory,
                httpContext.Connection.LocalIpAddress.ToString(),
                serverContext.HostHttpPort,
                serverContext.HostHttpsPort);

            if (nodeProvisioningEndpoint.RequireNodeObjects)
            {
                endpointContext.RkmNode = await serverContext.ConfigurationSource.GetRkmNodeByAttestationIdentityKeyPemAsync(
                    pem,
                    httpContext.RequestAborted);
                var authorizedNodeInfo = await AuthorizeNodeProvisioningEndpoint.CheckIfNodeAuthorizedAsync(endpointContext, endpointContext.RkmNode);
                if (authorizedNodeInfo == null)
                {
                    // CheckIfNodeAuthorizedAsync responded with "Unauthorized".
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(endpointContext.RkmNode?.Spec?.NodeGroup))
                {
                    endpointContext.RkmNodeGroup = await serverContext.ConfigurationSource.GetRkmNodeGroupAsync(
                        endpointContext.RkmNode.Spec.NodeGroup,
                        httpContext.RequestAborted);
                }
                if (!string.IsNullOrWhiteSpace(endpointContext.RkmNodeGroup?.Spec?.Provisioner))
                {
                    endpointContext.RkmNodeGroupProvisioner = await serverContext.ConfigurationSource.GetRkmNodeProvisionerAsync(
                        endpointContext.RkmNodeGroup.Spec.Provisioner,
                        serverContext.JsonSerializerContext.RkmNodeProvisioner,
                        httpContext.RequestAborted);
                }
                if (!string.IsNullOrWhiteSpace(endpointContext.RkmNode?.Status?.Provisioner?.Name))
                {
                    if (endpointContext.RkmNodeGroupProvisioner != null &&
                        endpointContext.RkmNode.Status.Provisioner.Name == endpointContext.RkmNodeGroupProvisioner.Metadata.Name)
                    {
                        endpointContext.RkmNodeProvisioner = endpointContext.RkmNodeGroupProvisioner;
                    }
                    else
                    {
                        endpointContext.RkmNodeProvisioner = await serverContext.ConfigurationSource.GetRkmNodeProvisionerAsync(
                            endpointContext.RkmNode.Status.Provisioner.Name,
                            serverContext.JsonSerializerContext.RkmNodeProvisioner,
                            httpContext.RequestAborted);
                    }
                }
            }

            await nodeProvisioningEndpoint.HandleRequestAsync(endpointContext);
            return true;
        }

        public async Task HandleRequestAsync(PxeBootServerContext serverContext, HttpContext httpContext)
        {
            _logger.LogInformation($"HTTP: Incoming request on {httpContext.Connection.LocalIpAddress} from {httpContext.Connection.RemoteIpAddress} for '{httpContext.Request.Path}'.");

            // @note: There's some weird bug in Hyper-V where the source IP address of connections from the VM to the host
            // can appear as the host's IP address. This is only dangerous when fetching the /autoexec.ipxe file during
            // boot, and can be mitigated by ensuring that the default switch network adapter is after the internal
            // network adapter. This scenario should never happen on real bare metal machines.
            if (!httpContext.Connection.RemoteIpAddress.Equals(httpContext.Connection.LocalIpAddress) ||
                (await httpContext.Connection.GetClientCertificateAsync(httpContext.RequestAborted)) != null)
            {
                if (await TryHandleAsUnauthenticatedFileTransfer(serverContext, httpContext))
                {
                    return;
                }
            }
            else
            {
                _logger.LogWarning("Non-HTTPS request from provisioner's own IP address is skipping unauthenticated file transfer handlers.");
            }

            if (await TryHandleAsNodeProvisioning(serverContext, httpContext))
            {
                return;
            }

            _logger.LogWarning($"HTTP: No handler found for '{httpContext.Request.Path}'.");
            httpContext.Response.StatusCode = 404;
            return;
        }
    }
}
