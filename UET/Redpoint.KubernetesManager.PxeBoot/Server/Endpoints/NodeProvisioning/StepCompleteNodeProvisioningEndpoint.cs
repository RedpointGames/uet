namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
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
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;

        public StepCompleteNodeProvisioningEndpoint(
            ILogger<StepCompleteNodeProvisioningEndpoint> logger,
            IServiceProvider serviceProvider,
            IEnumerable<IProvisioningStep> provisioningSteps)
                : base(serviceProvider)
        {
            _logger = logger;
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
                // The /step endpoint must be called first because this step hasn't started.
                _logger.LogInformation($"Step {context.RkmNode.Status.Provisioner.CurrentStepIndex} can't be completed, because it hasn't been started yet.");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
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
                context.MarkProvisioningCompleteForNode();
            }
            else
            {
                context.RkmNode.Status.Provisioner.CurrentStepStarted = !provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion);
            }

            await context.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                context.RkmNode.Status.AttestationIdentityKeyFingerprint!,
                $"Completed provisioning step '{currentStep!.Type}' at index {currentStepIndex}",
                context.CancellationToken);

            // If we completed the last step, return 204. Otherwise, serialize the 
            // next step as if /step had been called.
            if (context.RkmNode.Status.Provisioner == null || provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion))
            {
                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);

                // Client needs to call /step to get content.
                context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
            }
            else
            {
                var nextStep = context.RkmNodeProvisioner.Spec!.Steps[context.RkmNode.Status.Provisioner!.CurrentStepIndex!.Value];

                var nextProvisioningStep = _provisioningSteps[nextStep!.Type];

                _logger.LogInformation($"Provisioning: '{context.AikFingerprintShort}' is starting step '{nextStep!.Type}' at index {context.RkmNode.Status.Provisioner!.CurrentStepIndex!.Value}.");

                await nextProvisioningStep.ExecuteOnServerUncastedBeforeAsync(
                    nextStep!.DynamicSettings,
                    context.RkmNode.Status,
                    serverContext,
                    context.CancellationToken);

                if (nextProvisioningStep.Flags.HasFlag(ProvisioningStepFlags.SetAsRebootStepIndex))
                {
                    // Set the reboot step index if this is a reboot step. This must be done when a reboot step is
                    // started as part of /step-complete's next handling, since the reboot steps don't "complete" like other steps.
                    _logger.LogInformation($"Setting reboot step index to {context.RkmNode.Status.Provisioner.CurrentStepIndex}.");
                    context.RkmNode.Status.Provisioner.RebootStepIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex;
                    context.RkmNode.Status.Provisioner.RebootNotificationForOnceViaNotifyOccurred = null;
                }

                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);

                var nextStepSerialized = JsonSerializer.Serialize(nextStep, context.JsonSerializerContext.RkmNodeProvisionerStep);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Headers.Add("Content-Type", "application/json");
                using (var writer = new StreamWriter(context.Response.Body))
                {
                    await writer.WriteAsync(nextStepSerialized);
                }

                await context.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    context.RkmNode.Status.AttestationIdentityKeyFingerprint!,
                    $"Starting provisioning step '{nextStep!.Type}' at index {context.RkmNode.Status.Provisioner!.CurrentStepIndex!.Value}",
                    context.CancellationToken);
            }

            return;
        }
    }
}
