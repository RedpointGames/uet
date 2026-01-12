namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class StepCompleteNodeProvisioningEndpoint : StepBaseNodeProvisioningEndpoint
    {
        private readonly ILogger<StepCompleteNodeProvisioningEndpoint> _logger;
        private readonly IProvisioningStateManager _provisioningStateManager;
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;

        public StepCompleteNodeProvisioningEndpoint(
            ILogger<StepCompleteNodeProvisioningEndpoint> logger,
            IServiceProvider serviceProvider,
            IEnumerable<IProvisioningStep> provisioningSteps,
            IProvisioningStateManager provisioningStateManager)
                : base(serviceProvider)
        {
            _logger = logger;
            _provisioningStateManager = provisioningStateManager;
            _provisioningSteps = provisioningSteps.ToDictionary(k => k.Type, v => v);
        }

        public override string Path => "/step-complete";

        protected override async Task HandleStepRequestAsync(
            INodeProvisioningEndpointContext context,
            IProvisioningStepServerContext serverContext,
            RkmNodeProvisionerStep currentStep,
            IProvisioningStep provisioningStep)
        {
            if (!(context.RkmNode!.Status!.Provisioner!.CurrentStepStarted ?? false))
            {
                // @note: This can occur when the client is retrying /step-complete because it missed the result
                // from the first call while the server successfully processed it. Therefore, this is no longer
                // an error.
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                return;
            }

            _logger.LogInformation($"Provisioning: '{context.AikFingerprintShort}' is completing step '{currentStep!.Type}' at index {context.RkmNode.Status.Provisioner!.CurrentStepIndex!.Value}.");

            await provisioningStep.ExecuteOnServerUncastedAfterAsync(
                currentStep!.DynamicSettings,
                context.RkmNode.Status,
                serverContext,
                context.CancellationToken);
            var currentStepIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex;

            // Increment current step index and then update node status.
            if (!context.RkmNode.Status.Provisioner.CurrentStepIndex.HasValue)
            {
                context.RkmNode.Status.Provisioner.CurrentStepIndex = 0;
            }
            if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.CommitOnCompletion))
            {
                context.RkmNode.Status.Provisioner.LastStepCommittedIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex;
            }
            context.RkmNode.Status.Provisioner.CurrentStepIndex += 1;
            if (context.RkmNode.Status.Provisioner.CurrentStepIndex >= context.RkmNodeProvisioner!.Spec!.Steps!.Count)
            {
                _provisioningStateManager.MarkProvisioningCompleteForNode(context);
            }
            else
            {
                context.RkmNode.Status.Provisioner.CurrentStepStarted = false;
            }

            await context.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                context.RkmNode.Status.AttestationIdentityKeyFingerprint!,
                $"Completed provisioning step '{currentStep!.Type}' at index {currentStepIndex}",
                context.CancellationToken);

            // @note: We previously allowed this endpoint to return the next step and implicitly start it. However, if the client
            // times out on this call and retries, it can cause the client to skip steps because the server does not know the client
            // missed the first /step-complete result.
            //
            // We could avoid this by having the client send some kind of idempotency key or tell the server what step it thinks
            // it is finishing... or we could just always return 204 and make clients call /step to start the next step.

            await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                context.AikFingerprint,
                context.RkmNode.Status,
                context.CancellationToken);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            return;
        }
    }
}
