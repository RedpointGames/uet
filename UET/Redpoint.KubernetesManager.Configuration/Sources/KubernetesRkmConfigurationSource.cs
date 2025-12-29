namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s;
    using k8s.Models;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public class KubernetesRkmConfigurationSource : IRkmConfigurationSource
    {
        private readonly IKubernetes _kubernetes;

        public KubernetesRkmConfigurationSource(
            IKubernetes kubernetes)
        {
            _kubernetes = kubernetes;
        }

        public Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            RkmNodeStatus status,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<RkmConfiguration> GetRkmConfigurationAsync(CancellationToken cancellationToken)
        {
            var configuration = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync(
                "rkm.redpoint.games",
                "v1",
                "rkmconfigurations",
                "default",
                cancellationToken);
            if (configuration == null)
            {
                // Provide a default configuration in case one hasn't been deployed by
                // RKM itself, or it's been deleted.
                return new RkmConfiguration
                {
                    ApiVersion = "rkm.redpoint.games/v1",
                    Kind = "RkmConfiguration",
                    Metadata = new V1ObjectMeta
                    {
                        Name = "default"
                    },
                    Spec = new RkmConfigurationSpec
                    {
                        ComponentVersions = new RkmConfigurationComponentVersions
                        {
                            Rkm = "2025.1364.398",
                            Containerd = "2.2.1",
                            Runc = "1.3.4",
                            Kubernetes = "1.35.0",
                            Etcd = "3.6.7",
                            CniPlugins = "1.9.0",
                            Flannel = "0.27.4",
                            FlannelCniSuffix = "-flannel1",
                        }
                    }
                };
            }

            return JsonSerializer.Deserialize(
                (JsonElement)configuration,
                KubernetesRkmJsonSerializerContext.Default.RkmConfiguration)!;
        }

        public async Task ReplaceRkmConfigurationAsync(RkmConfiguration configuration, CancellationToken cancellationToken)
        {
            await _kubernetes.CustomObjects.ReplaceClusterCustomObjectAsync(
                JsonSerializer.SerializeToElement(
                    configuration,
                    KubernetesRkmJsonSerializerContext.Default.RkmConfiguration),
                "rkm.redpoint.games",
                "v1",
                "rkmconfigurations",
                "default",
                cancellationToken: cancellationToken);
        }
    }
}
