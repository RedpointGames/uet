namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class AuthorizeNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        private readonly IVariableProvider _variableProvider;
        private readonly ILogger<AuthorizeNodeProvisioningEndpoint> _logger;

        public AuthorizeNodeProvisioningEndpoint(
            IVariableProvider variableProvider,
            ILogger<AuthorizeNodeProvisioningEndpoint> logger)
        {
            _variableProvider = variableProvider;
            _logger = logger;
        }

        public string Path => "/authorize";

        public bool RequireNodeObjects => false;

        private static async Task RespondWithUnauthorizedAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }

        public class AuthorizedNodeInfo
        {
            public required RkmNodeGroup RkmNodeGroup { get; set; }

            public required RkmNodeProvisioner RkmNodeProvisioner { get; set; }
        }

        public static async Task<AuthorizedNodeInfo?> CheckIfNodeAuthorizedAsync(
            INodeProvisioningEndpointContext context,
            RkmNode? node)
        {
            if (!(node?.Spec?.Authorized ?? false) ||
                string.IsNullOrWhiteSpace(node?.Spec?.NodeName))
            {
                await RespondWithUnauthorizedAsync(context);
                return null;
            }

            if (string.IsNullOrWhiteSpace(node?.Spec?.NodeGroup))
            {
                // Not yet authorized, or not configured properly.
                await RespondWithUnauthorizedAsync(context);
                return null;
            }

            var nodeGroup = await context.ConfigurationSource.GetRkmNodeGroupAsync(
                node.Spec.NodeGroup,
                context.CancellationToken);
            if (nodeGroup?.Spec?.Provisioner == null)
            {
                // Not yet authorized, or not configured properly.
                await RespondWithUnauthorizedAsync(context);
                return null;
            }

            var nodeProvisioner = await context.ConfigurationSource.GetRkmNodeProvisionerAsync(
                nodeGroup.Spec.Provisioner,
                context.JsonSerializerContext.RkmNodeProvisioner,
                context.CancellationToken);
            if (nodeProvisioner == null)
            {
                // Not yet authorized, or not configured properly.
                await RespondWithUnauthorizedAsync(context);
                return null;
            }

            return new AuthorizedNodeInfo
            {
                RkmNodeGroup = nodeGroup,
                RkmNodeProvisioner = nodeProvisioner,
            };
        }

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            var request = (await JsonSerializer.DeserializeAsync(
                context.Request.Body,
                ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeRequest,
                context.CancellationToken))!;

            var candidateNode = await context.ConfigurationSource.CreateOrUpdateRkmNodeByAttestationIdentityKeyPemAsync(
                context.AikPem,
                [RkmNodeRole.Worker],
                false,
                request.CapablePlatforms,
                request.Architecture,
                context.CancellationToken);
            var authorizedNodeInfo = await CheckIfNodeAuthorizedAsync(context, candidateNode);
            if (authorizedNodeInfo == null)
            {
                return;
            }

            var parameterValues = _variableProvider.ComputeParameterValuesNodeProvisioningEndpoint(
                ServerSideVariableContext.FromNodeProvisionerWithoutContextLoadedObjects(
                    context,
                    candidateNode,
                    authorizedNodeInfo.RkmNodeGroup,
                    authorizedNodeInfo.RkmNodeProvisioner));

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new AuthorizeNodeResponse
                {
                    NodeName = candidateNode.Spec!.NodeName!,
                    AikFingerprint = context.AikFingerprint,
                    ParameterValues = parameterValues,
                },
                ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeResponse,
                context.CancellationToken);
        }
    }
}
