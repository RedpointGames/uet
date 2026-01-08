namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    internal abstract class StepBaseNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;
        private readonly IProvisionerHasher _provisionerHasher;

        public StepBaseNodeProvisioningEndpoint(
            IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<StepBaseNodeProvisioningEndpoint>>();
            _provisioningSteps = serviceProvider.GetServices<IProvisioningStep>().ToDictionary(k => k.Type, v => v);
            _provisionerHasher = serviceProvider.GetRequiredService<IProvisionerHasher>();
        }

        public abstract string Path { get; }

        public bool RequireNodeObjects => true;

        private bool ShouldResetProvisionState(INodeProvisioningEndpointContext context)
        {
            if (context.RkmNode!.Spec?.ForceReprovision ?? false)
            {
                _logger.LogInformation($"{context.RkmNode.Metadata.Name} has force reprovision requested.");
                return true;
            }

            if (context.RkmNode!.Status?.LastSuccessfulProvision == null &&
                context.RkmNode!.Status?.Provisioner == null)
            {
                _logger.LogInformation($"{context.RkmNode.Metadata.Name} has no last successful provision.");
                return true;
            }

            if (context.RkmNodeGroup != null &&
                context.RkmNodeGroupProvisioner != null &&
                context.RkmNodeProvisioner != null)
            {
                if (context.RkmNodeGroupProvisioner.Metadata.Name != context.RkmNodeProvisioner.Metadata.Name)
                {
                    _logger.LogInformation($"{context.RkmNode.Metadata.Name} group changed from provisioner '{context.RkmNodeProvisioner.Metadata.Name}' to provisioner '{context.RkmNodeGroupProvisioner.Metadata.Name}' during provision.");
                    return true;
                }

                var groupProvisionerStepsHash = _provisionerHasher.GetProvisionerHash(
                    ServerSideVariableContext.FromNodeGroupProvisioner(context));
                if (groupProvisionerStepsHash != context.RkmNode.Status.Provisioner?.Hash)
                {
                    _logger.LogInformation($"{context.RkmNode.Metadata.Name} group assigned provisioner '{context.RkmNodeGroupProvisioner.Metadata.Name}' now has hash '{groupProvisionerStepsHash}', changed from hash '{context.RkmNode.Status.Provisioner?.Hash}' during provision.");
                    return true;
                }
            }

            if (context.RkmNodeGroup != null &&
                context.RkmNodeGroupProvisioner != null &&
                context.RkmNode.Status.LastSuccessfulProvision != null)
            {
                if (context.RkmNodeGroupProvisioner.Metadata.Name != context.RkmNode.Status.LastSuccessfulProvision.Name)
                {
                    _logger.LogInformation($"{context.RkmNode.Metadata.Name} group changed from provisioner '{context.RkmNode.Status.LastSuccessfulProvision.Name}' to provisioner '{context.RkmNodeGroupProvisioner.Metadata.Name}' since last successful provision.");
                    return true;
                }

                var groupProvisionerStepsHash = _provisionerHasher.GetProvisionerHash(
                    ServerSideVariableContext.FromNodeGroupProvisioner(context));
                if (groupProvisionerStepsHash != context.RkmNode.Status.LastSuccessfulProvision.Hash)
                {
                    _logger.LogInformation($"{context.RkmNode.Metadata.Name} group assigned provisioner '{context.RkmNodeGroupProvisioner.Metadata.Name}' now has hash '{groupProvisionerStepsHash}', changed from hash '{context.RkmNode.Status.LastSuccessfulProvision.Hash}' since last successful provision.");
                    return true;
                }
            }

            return false;
        }

        private static async Task RespondWithRebootAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }

        private static async Task RespondWithMisconfiguredAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }

        private static Task RespondWithProvisionCompleteAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        private async Task<bool> ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(INodeProvisioningEndpointContext context, bool forceReboot)
        {
            _logger.LogInformation($"Provisioning state is resetting for node {context.RkmNode!.Metadata.Name}.");
            context.RkmNode.Status ??= new();
            context.RkmNode.Status.BootToDisk = false;
            context.RkmNode.Status.LastSuccessfulProvision = null;
            context.RkmNode.Spec ??= new();
            var requireReboot = context.RkmNode.Status.Provisioner != null;
            if (context.RkmNodeGroupProvisioner != null && context.RkmNodeGroup != null)
            {
                context.RkmNode.Status.Provisioner = new RkmNodeStatusProvisioner
                {
                    Name = context.RkmNodeGroupProvisioner.Metadata.Name,
                    Hash = _provisionerHasher.GetProvisionerHash(
                        ServerSideVariableContext.FromNodeGroupProvisioner(context)),
                    CurrentStepIndex = null,
                    CurrentStepStarted = false,
                    LastStepCommittedIndex = null,
                    RebootStepIndex = null,
                    RebootNotificationForOnceViaNotifyOccurred = null,
                };
                context.RkmNodeProvisioner = context.RkmNodeGroupProvisioner;
            }
            if (context.RkmNode.Spec.ForceReprovision)
            {
                _logger.LogInformation("Turning off 'force provision' flag...");
                context.RkmNode.Spec.ForceReprovision = false;
                await context.ConfigurationSource.UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Spec.ForceReprovision,
                    context.CancellationToken);
            }

            // If the node is in an unknown state (i.e. it hasn't just booted into the default initrd environment), then
            // it's possible we're hitting this state mid-provision or while the node is in a different environment. Update
            // the node state in configuration, and then tell the node it needs to immediately reboot.
            if (requireReboot || forceReboot)
            {
                _logger.LogWarning($"Provisioning state changed while provisioning was already happening for {context.RkmNode!.Metadata.Name}. Telling the node it needs to reboot!");
                context.UpdateRegisteredIpAddressesForNode();
                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);
                await RespondWithRebootAsync(context);
                return true;
            }

            return false;
        }

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            // Check if we should restart provisioning.
            if (ShouldResetProvisionState(context))
            {
                if (context.RkmNodeGroup == null)
                {
                    _logger.LogError($"Provisioning state needs to reset for node {context.RkmNode!.Metadata.Name}, but this node has no valid node group assigned!");
                    await RespondWithMisconfiguredAsync(context);
                    return;
                }

                if (context.RkmNodeGroupProvisioner == null)
                {
                    _logger.LogError($"Provisioning state needs to reset for node {context.RkmNode!.Metadata.Name}, but this node's group has no valid provisioner assigned!");
                    await RespondWithMisconfiguredAsync(context);
                    return;
                }

                if (await ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(context, false))
                {
                    return;
                }
            }

            // Check if there is nothing to provision.
            if (string.IsNullOrWhiteSpace(context.RkmNode!.Status?.Provisioner?.Name) ||
                context.RkmNodeProvisioner == null)
            {
                await RespondWithProvisionCompleteAsync(context);
                return;
            }

            // Check if we've completed provisioning.
            var currentProvisionerStepCount = context.RkmNodeProvisioner.Spec?.Steps?.Count ?? 0;
            var currentStepIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex ?? 0;
            if (currentProvisionerStepCount <= currentStepIndex)
            {
                _logger.LogInformation($"Node {context.RkmNode!.Metadata.Name} completed provisioning.");

                context.MarkProvisioningCompleteForNode();

                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);

                await RespondWithProvisionCompleteAsync(context);
                return;
            }

            // If this is the initial request after boot, check that the node booted into the correct environment.
            var isInitial = context.Request.Query.TryGetValue("initial", out var initial) && initial.FirstOrDefault() == "true";
            if (isInitial)
            {
                if (!context.Request.Query.TryGetValue("bootedFromStepIndex", out var bootedFromStepIndex) ||
                    string.IsNullOrWhiteSpace(bootedFromStepIndex) ||
                    int.Parse(bootedFromStepIndex!, CultureInfo.InvariantCulture) != (context.RkmNode.Status.Provisioner.RebootStepIndex ?? -1))
                {
                    // The machine didn't boot with the expected autoexec.ipxe script, usually because
                    // the IP address of the machine during PXE boot and the IP address of the machine
                    // during provisioning inside initrd is different. When this happens, autoexec.ipxe isn't
                    // serving the desired script (which could result in the following provisioning steps
                    // running in the complete wrong environment).
                    //
                    // In this case, reset provisioning and force the machine to reboot.
                    _logger.LogError($"Machine should have booted from step index {(context.RkmNode.Status.Provisioner.RebootStepIndex ?? -1)}, but booted from {bootedFromStepIndex} instead.");
                    await ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(context, true);
                    return;
                }

                // Reset the "currentStepIndex" to the index after "lastStepCommittedIndex". This ensures
                // that step completion status don't carry over reboots unless explicitly committed.
                var lastStepCommittedIndex = context.RkmNode.Status.Provisioner.LastStepCommittedIndex ?? -1;
                var rebootStepIndex = context.RkmNode.Status.Provisioner.RebootStepIndex ?? -1;
                if (lastStepCommittedIndex < rebootStepIndex)
                {
                    // Last committed step must always at least be the reboot step index.
                    lastStepCommittedIndex = rebootStepIndex;
                }
                _logger.LogInformation($"Setting current step to {lastStepCommittedIndex + 1} during initial step fetch. Last committed step index is {lastStepCommittedIndex}, reboot step index is {rebootStepIndex}.");
                context.RkmNode.Status.Provisioner.CurrentStepIndex = lastStepCommittedIndex + 1;
                context.UpdateRegisteredIpAddressesForNode();
            }

            var currentStep = context.RkmNodeProvisioner.Spec!.Steps![currentStepIndex];
            var provisioningStepImpl = _provisioningSteps[currentStep!.Type];

            var serverContext = new HttpContextProvisioningStepServerContext(context.HttpContext);

            await HandleStepRequestAsync(
                context,
                serverContext,
                currentStep,
                provisioningStepImpl);
        }

        protected abstract Task HandleStepRequestAsync(
            INodeProvisioningEndpointContext context,
            IProvisioningStepServerContext serverContext,
            RkmNodeProvisionerStep currentStep,
            IProvisioningStep provisioningStepImpl);
    }
}
