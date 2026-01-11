namespace Redpoint.KubernetesManager.PxeBoot.Provisioning
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class RkmNodeRelatedObjects
    {
        public required RkmNodeGroup? RkmNodeGroup { get; init; }

        public required RkmNodeProvisioner? RkmNodeGroupProvisioner { get; init; }

        public required RkmNodeProvisioner? RkmNodeProvisioner { get; init; }
    }

    internal interface IRelatedObjectLoader
    {
        Task<RkmNodeRelatedObjects> LoadRelatedObjectsAsync(
            IRkmConfigurationSource configurationSource,
            RkmNode rkmNode,
            KubernetesRkmJsonSerializerContext jsonSerializerContext,
            CancellationToken cancellationToken);
    }

    internal class DefaultRelatedObjectLoader : IRelatedObjectLoader
    {
        public async Task<RkmNodeRelatedObjects> LoadRelatedObjectsAsync(
            IRkmConfigurationSource configurationSource,
            RkmNode rkmNode,
            KubernetesRkmJsonSerializerContext jsonSerializerContext,
            CancellationToken cancellationToken)
        {
            RkmNodeGroup? rkmNodeGroup = null;
            RkmNodeProvisioner? rkmNodeGroupProvisioner = null;
            RkmNodeProvisioner? rkmNodeProvisioner = null;

            if (!string.IsNullOrWhiteSpace(rkmNode?.Spec?.NodeGroup))
            {
                rkmNodeGroup = await configurationSource.GetRkmNodeGroupAsync(
                    rkmNode.Spec.NodeGroup,
                    cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(rkmNodeGroup?.Spec?.Provisioner))
            {
                rkmNodeGroupProvisioner = await configurationSource.GetRkmNodeProvisionerAsync(
                    rkmNodeGroup.Spec.Provisioner,
                    jsonSerializerContext.RkmNodeProvisioner,
                    cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(rkmNode?.Status?.Provisioner?.Name))
            {
                if (rkmNodeGroupProvisioner != null &&
                    rkmNode.Status.Provisioner.Name == rkmNodeGroupProvisioner.Metadata.Name)
                {
                    rkmNodeProvisioner = rkmNodeGroupProvisioner;
                }
                else
                {
                    rkmNodeProvisioner = await configurationSource.GetRkmNodeProvisionerAsync(
                        rkmNode.Status.Provisioner.Name,
                        jsonSerializerContext.RkmNodeProvisioner,
                        cancellationToken);
                }
            }

            return new RkmNodeRelatedObjects
            {
                RkmNodeGroup = rkmNodeGroup,
                RkmNodeGroupProvisioner = rkmNodeGroupProvisioner,
                RkmNodeProvisioner = rkmNodeProvisioner,
            };
        }
    }
}
