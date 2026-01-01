namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    public class TestRkmConfigurationSource : IRkmConfigurationSource
    {
        private readonly ILogger<TestRkmConfigurationSource> _logger;

        private Dictionary<string, RkmNode> _nodes = new();

        public TestRkmConfigurationSource(ILogger<TestRkmConfigurationSource> logger)
        {
            _logger = logger;
        }

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
                    Architecture = "amd64",
                    Provisioner = new RkmNodeStatusProvisioner
                    {
                        Name = "default",
                        Hash = string.Empty,
                        CurrentStepIndex = 0,
                    }
                },
            };
            _nodes.Add(fingerprint, value);

            return Task.FromResult<RkmNode?>(value);
        }

        public Task<RkmNode?> GetRkmNodeByRegisteredIpAddressAsync(string registeredIpAddress, CancellationToken cancellationToken)
        {
            foreach (var node in _nodes.Values)
            {
                foreach (var ipAddress in (node.Status?.RegisteredIpAddresses ?? []))
                {
                    if (ipAddress.Address == registeredIpAddress &&
                        ipAddress.ExpiresAt.HasValue &&
                        ipAddress.ExpiresAt.Value > DateTimeOffset.UtcNow)
                    {
                        return Task.FromResult<RkmNode?>(node);
                    }
                }
            }

            return Task.FromResult<RkmNode?>(null);
        }

        public Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(string attestationIdentityKeyFingerprint, RkmNodeStatus status, CancellationToken cancellationToken)
        {
            if (_nodes.TryGetValue(attestationIdentityKeyFingerprint, out var value))
            {
                value.Status = status;
            }

            _logger.LogTrace(JsonSerializer.Serialize(status, new KubernetesRkmJsonSerializerContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new KubernetesDateTimeOffsetConverter(),
                }
            }).RkmNodeStatus));

            return Task.CompletedTask;
        }

        public async Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            JsonTypeInfo<RkmNodeProvisionerSpec> jsonTypeInfoWithSerializerForSteps,
            CancellationToken cancellationToken)
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
                        jsonTypeInfoWithSerializerForSteps,
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
