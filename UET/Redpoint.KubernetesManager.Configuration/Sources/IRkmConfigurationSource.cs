namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IRkmConfigurationSource
    {
        Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            CancellationToken cancellationToken);

        Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            RkmNodeStatus status,
            CancellationToken cancellationToken);

        Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            CancellationToken cancellationToken);

        Task<RkmConfiguration> GetRkmConfigurationAsync(
            CancellationToken cancellationToken);

        Task ReplaceRkmConfigurationAsync(
            RkmConfiguration configuration,
            CancellationToken cancellationToken);
    }
}
