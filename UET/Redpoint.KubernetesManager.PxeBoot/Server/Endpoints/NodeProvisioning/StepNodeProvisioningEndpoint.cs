namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class StepNodeProvisioningEndpoint : StepBaseNodeProvisioningEndpoint
    {
        private readonly ILogger<StepNodeProvisioningEndpoint> _logger;

        public StepNodeProvisioningEndpoint(
            ILogger<StepNodeProvisioningEndpoint> logger,
            IServiceProvider serviceProvider)
                : base(serviceProvider)
        {
            _logger = logger;
        }

        public override string Path => "/step";

        protected override async Task HandleStepRequestAsync(
            INodeProvisioningEndpointContext context,
            IProvisioningStepServerContext serverContext,
            RkmNodeProvisionerStep currentStep,
            IProvisioningStep provisioningStep)
        {
            if (!(context.RkmNode!.Status!.Provisioner!.CurrentStepStarted ?? false))
            {
                _logger.LogInformation($"Provisioning: '{context.AikFingerprintShort}' is starting step '{currentStep!.Type}' at index {context.RkmNode.Status.Provisioner!.CurrentStepIndex!.Value}.");

                await provisioningStep.ExecuteOnServerUncastedBeforeAsync(
                    currentStep!.DynamicSettings,
                    context.RkmNode.Status,
                    serverContext,
                    context.CancellationToken);

                context.RkmNode.Status.Provisioner.CurrentStepStarted = true;

                if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.SetAsRebootStepIndex))
                {
                    // Set the reboot step index if this is a reboot step. This must be done during /step, since
                    // the reboot steps don't "complete" like other steps.
                    _logger.LogInformation($"Setting reboot step index to {context.RkmNode.Status.Provisioner.CurrentStepIndex}.");
                    context.RkmNode.Status.Provisioner.RebootStepIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex;
                    context.RkmNode.Status.Provisioner.RebootNotificationForOnceViaNotifyOccurred = null;
                }

                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);
            }

            // Serialize the current step to the client.
            // @todo: Replace variables in step config...
            var currentStepSerialized = JsonSerializer.Serialize(currentStep, context.JsonSerializerContext.RkmNodeProvisionerStep);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Headers.Add("Content-Type", "application/json");
            using (var writer = new StreamWriter(context.Response.Body))
            {
                await writer.WriteAsync(currentStepSerialized);
            }
            return;
        }
    }
}
