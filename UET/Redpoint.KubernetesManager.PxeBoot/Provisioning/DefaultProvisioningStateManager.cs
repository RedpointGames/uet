namespace Redpoint.KubernetesManager.PxeBoot.Provisioning
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultProvisioningStateManager : IProvisioningStateManager
    {
        private readonly IProvisionerHasher _provisionerHasher;
        private readonly ILogger<DefaultProvisioningStateManager> _logger;

        public DefaultProvisioningStateManager(
            IProvisionerHasher provisionerHasher,
            ILogger<DefaultProvisioningStateManager> logger)
        {
            _provisionerHasher = provisionerHasher;
            _logger = logger;
        }

        public async Task<ProvisioningResponse> UpdateStateAsync(
            INodeProvisioningContext context,
            bool canClearForceReprovisionFlag)
        {
            // Check if we should restart provisioning.
            if (ShouldResetProvisionState(context))
            {
                if (context.RkmNodeGroup == null)
                {
                    _logger.LogError($"Provisioning state needs to reset for node {context.RkmNode!.Metadata.Name}, but this node has no valid node group assigned!");
                    return ProvisioningResponse.Misconfigured;
                }

                if (context.RkmNodeGroupProvisioner == null)
                {
                    _logger.LogError($"Provisioning state needs to reset for node {context.RkmNode!.Metadata.Name}, but this node's group has no valid provisioner assigned!");
                    return ProvisioningResponse.Misconfigured;
                }

                if (await ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(context, false, canClearForceReprovisionFlag))
                {
                    return ProvisioningResponse.Reboot;
                }
            }

            // Check if there is nothing to provision.
            if (string.IsNullOrWhiteSpace(context.RkmNode!.Status?.Provisioner?.Name) ||
                context.RkmNodeProvisioner == null)
            {
                return ProvisioningResponse.Complete;
            }

            // Check if we've completed provisioning.
            var currentProvisionerStepCount = context.RkmNodeProvisioner.Spec?.Steps?.Count ?? 0;
            var currentStepIndex = context.RkmNode.Status.Provisioner.CurrentStepIndex ?? 0;
            if (currentProvisionerStepCount <= currentStepIndex)
            {
                _logger.LogInformation($"Node {context.RkmNode!.Metadata.Name} completed provisioning.");

                MarkProvisioningCompleteForNode(context);

                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);

                return ProvisioningResponse.Complete;
            }

            return ProvisioningResponse.Provisioning;
        }

        public void UpdateRegisteredIpAddressesForNode(
            INodeProvisioningContext context)
        {
            if (context.RkmNode?.Status == null)
            {
                return;
            }

            var threshold = DateTimeOffset.UtcNow;
            var newExpiry = DateTimeOffset.UtcNow.AddDays(1);

            var addresses = new List<IPAddress>
            {
                context.RemoteIpAddress
            };
            if (context.RemoteIpAddress.IsIPv4MappedToIPv6)
            {
                addresses.Add(context.RemoteIpAddress.MapToIPv4());
            }

            context.RkmNode.Status.RegisteredIpAddresses ??= new List<RkmNodeStatusRegisteredIpAddress>();
            context.RkmNode.Status.RegisteredIpAddresses.RemoveAll(x => !x.ExpiresAt.HasValue || x.ExpiresAt.Value < threshold);

            foreach (var addressRaw in addresses)
            {
                var address = addressRaw.ToString();

                var existingEntry = context.RkmNode.Status.RegisteredIpAddresses.FirstOrDefault(x => x.Address == address);
                if (existingEntry != null)
                {
                    _logger.LogInformation($"Updating existing expiry of registered IP address '{address}' to {newExpiry}...");
                    existingEntry.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
                }
                else
                {
                    _logger.LogInformation($"Adding new entry for registered IP address '{address}' with expiry {newExpiry}...");
                    context.RkmNode.Status.RegisteredIpAddresses.Add(new RkmNodeStatusRegisteredIpAddress
                    {
                        Address = address,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                    });
                }
            }
        }

        public void MarkProvisioningCompleteForNode(
            INodeProvisioningContext context)
        {
            if (context.RkmNode?.Status?.Provisioner == null)
            {
                return;
            }

            context.RkmNode.Status.LastSuccessfulProvision = new RkmNodeStatusLastSuccessfulProvision
            {
                Name = context.RkmNode.Status.Provisioner.Name,
                Hash = context.RkmNode.Status.Provisioner.Hash,
            };
            context.RkmNode.Status.Provisioner = null;
        }

        private bool ShouldResetProvisionState(INodeProvisioningContext context)
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

        public async Task<bool> ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(
            INodeProvisioningContext context,
            bool forceReboot,
            bool canClearForceReprovisionFlag)
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
            if (context.RkmNode.Spec.ForceReprovision &&
                canClearForceReprovisionFlag)
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
                UpdateRegisteredIpAddressesForNode(context);
                await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    context.RkmNode.Status,
                    context.CancellationToken);
                return true;
            }

            return false;
        }

    }
}
