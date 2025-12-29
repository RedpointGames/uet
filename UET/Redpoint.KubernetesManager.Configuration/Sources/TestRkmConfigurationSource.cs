namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s.Models;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public class TestRkmConfigurationSource : IRkmConfigurationSource
    {
        private Dictionary<string, RkmNode> _nodes = new();

        public Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(string attestationIdentityKeyPem, CancellationToken cancellationToken)
        {
            var fingerprint = RkmNodeFingerprint.CreateFromPem(attestationIdentityKeyPem);

            if (_nodes.TryGetValue(fingerprint, out var value))
            {
                return Task.FromResult<RkmNode?>(value);
            }

            value = new RkmNode
            {
                ApiVersion = "rkm.redpoint.games/v1",
                Kind = "RkmNode",
                Metadata = new V1ObjectMeta
                {
                    Name = fingerprint.Substring(0, 8),
                },
                Spec = new RkmNodeSpec
                {
                    NodeGroup = string.Empty,
                    NodeName = "test-node",
                    Authorized = true,
                    ForceReprovision = false,
                },
                Status = new RkmNodeStatus
                {
                    Roles = [RkmNodeRole.Worker],
                    Immutable = false,
                    AttestationIdentityKeyFingerprint = fingerprint,
                    AttestationIdentityKeyPem = attestationIdentityKeyPem,
                    FirstSeen = DateTimeOffset.UtcNow,
                    MostRecentJoinRequest = DateTimeOffset.UtcNow,
                    CapablePlatforms = [RkmNodePlatform.Windows, RkmNodePlatform.Linux],
                    Architecture = "amd64"
                },
            };
            _nodes.Add(fingerprint, value);

            return Task.FromResult<RkmNode?>(value);
        }

        public Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(string attestationIdentityKeyFingerprint, RkmNodeStatus status, CancellationToken cancellationToken)
        {
            if (_nodes.TryGetValue(attestationIdentityKeyFingerprint, out var value))
            {
                value.Status = status;
            }

            return Task.CompletedTask;
        }

        public async Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(string name, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream("provisioner.json", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return new RkmNodeProvisioner
                {
                    ApiVersion = "rkm.redpoint.games/v1",
                    Kind = "RkmNodeProvisioner",
                    Metadata = new V1ObjectMeta
                    {
                        Name = "default",
                    },
                    Spec = await JsonSerializer.DeserializeAsync(
                        stream,
                        KubernetesRkmJsonSerializerContext.Default.RkmNodeProvisionerSpec,
                        cancellationToken)
                };
            }
        }

        public Task<RkmConfiguration> GetRkmConfigurationAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ReplaceRkmConfigurationAsync(RkmConfiguration configuration, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
