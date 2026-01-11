namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.YamlToJson;
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
    using YamlDotNet.Core;
    using YamlDotNet.Core.Events;
    using YamlDotNet.RepresentationModel;
    using YamlDotNet.Serialization;

    public class TestRkmConfigurationSource : IRkmConfigurationSource
    {
        private readonly ILogger<TestRkmConfigurationSource> _logger;

        private Dictionary<string, RkmNode> _nodes = new();

        public TestRkmConfigurationSource(ILogger<TestRkmConfigurationSource> logger)
        {
            _logger = logger;
        }

        public Task CreateProvisioningEventForRkmNodeAsync(
            string attestationIdentityKeyFingerprint,
            string message,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<RkmNode> CreateOrUpdateRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            RkmNodeRole[] roles,
            bool immutable,
            IList<RkmNodePlatform> capablePlatforms,
            string architecture,
            CancellationToken cancellationToken)
        {
            var fingerprint = RkmNodeFingerprint.CreateFromPem(attestationIdentityKeyPem);

            if (!_nodes.TryGetValue(fingerprint, out var value))
            {
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
                        NodeName = string.Empty,
                        Authorized = false,
                        ForceReprovision = true,
                    },
                    Status = new RkmNodeStatus
                    {
                        Roles = null,
                        Immutable = false,
                        AttestationIdentityKeyFingerprint = fingerprint,
                        AttestationIdentityKeyPem = attestationIdentityKeyPem,
                        FirstSeen = DateTimeOffset.UtcNow,
                        MostRecentJoinRequest = null,
                        CapablePlatforms = null,
                        Architecture = null,
                    },
                };
                _nodes.Add(fingerprint, value);
            }

            value.Spec ??= new();
            value.Status ??= new();

            value.Status.Roles = roles;
            value.Status.Immutable = immutable;
            value.Status.MostRecentJoinRequest = DateTimeOffset.UtcNow;
            value.Status.CapablePlatforms = [.. capablePlatforms];
            value.Status.Architecture = architecture;

            // @note: The test configuration immediately authorizes nodes and sets them
            // up for provisioning.
            if (value.Spec.ForceReprovision)
            {
                value.Spec.ForceReprovision = false;
                value.Spec.Authorized = true;
                value.Spec.NodeName = "test-node";
                value.Status.Provisioner = new RkmNodeStatusProvisioner
                {
                    Name = "default",
                    Hash = string.Empty,
                    CurrentStepIndex = 0,
                };
            }

            return Task.FromResult(value);
        }

        public Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(string attestationIdentityKeyPem, CancellationToken cancellationToken)
        {
            var fingerprint = RkmNodeFingerprint.CreateFromPem(attestationIdentityKeyPem);

            if (_nodes.TryGetValue(fingerprint, out var value))
            {
                return Task.FromResult<RkmNode?>(value);
            }

            return Task.FromResult<RkmNode?>(null);
        }

        public Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyFingerprintAsync(string attestationIdentityKeyFingerprint, CancellationToken cancellationToken)
        {
            if (_nodes.TryGetValue(attestationIdentityKeyFingerprint, out var value))
            {
                return Task.FromResult<RkmNode?>(value);
            }

            return Task.FromResult<RkmNode?>(null);
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

            _logger.LogTrace(JsonSerializer.Serialize(status, KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNodeStatus));

            return Task.CompletedTask;
        }

        public Task UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(string attestationIdentityKeyFingerprint, bool forceReprovision, CancellationToken cancellationToken)
        {
            if (_nodes.TryGetValue(attestationIdentityKeyFingerprint, out var value))
            {
                value.Spec ??= new();
                value.Spec.ForceReprovision = forceReprovision;
            }

            return Task.CompletedTask;
        }

        public async Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            JsonTypeInfo<RkmNodeProvisioner> jsonTypeInfoWithSerializerForSteps,
            CancellationToken cancellationToken)
        {
            using (var stream = new FileStream("provisioner.yaml", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var targetStream = new MemoryStream();
                YamlToJsonConverter.Convert(stream, targetStream);
                targetStream.Seek(0, SeekOrigin.Begin);

                return await JsonSerializer.DeserializeAsync(
                    targetStream,
                    jsonTypeInfoWithSerializerForSteps,
                    cancellationToken);
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

        public Task<RkmNodeGroup?> GetRkmNodeGroupAsync(string name, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
