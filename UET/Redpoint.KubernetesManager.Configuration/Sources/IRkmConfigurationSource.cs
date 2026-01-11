namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    public interface IRkmConfigurationSource
    {
        Task CreateProvisioningEventForRkmNodeAsync(
            string attestationIdentityKeyFingerprint,
            string message,
            CancellationToken cancellationToken);

        Task<RkmNode> CreateOrUpdateRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            RkmNodeRole[] roles,
            bool immutable,
            IList<RkmNodePlatform> capablePlatforms,
            string architecture,
            CancellationToken cancellationToken);

        Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            CancellationToken cancellationToken);

        Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            CancellationToken cancellationToken);

        Task<RkmNode?> GetRkmNodeByRegisteredIpAddressAsync(
            string registeredIpAddress,
            CancellationToken cancellationToken);

        Task<RkmNodeGroup?> GetRkmNodeGroupAsync(
            string name,
            CancellationToken cancellationToken);

        Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            RkmNodeStatus status,
            CancellationToken cancellationToken);

        Task UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            bool forceReprovision,
            CancellationToken cancellationToken);

        Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            JsonTypeInfo<RkmNodeProvisioner> jsonTypeInfoWithSerializerForSteps,
            CancellationToken cancellationToken);

        Task<RkmConfiguration> GetRkmConfigurationAsync(
            CancellationToken cancellationToken);

        Task ReplaceRkmConfigurationAsync(
            RkmConfiguration configuration,
            CancellationToken cancellationToken);
    }
}
