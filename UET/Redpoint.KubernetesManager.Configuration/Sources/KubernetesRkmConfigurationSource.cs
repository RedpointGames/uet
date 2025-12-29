namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s;
    using k8s.Autorest;
    using k8s.Models;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using YamlDotNet.Core.Tokens;

    public class KubernetesRkmConfigurationSource : IRkmConfigurationSource
    {
        private readonly IKubernetes _kubernetes;

        public KubernetesRkmConfigurationSource(
            IKubernetes kubernetes)
        {
            _kubernetes = kubernetes;
        }

        public async Task<RkmNode> CreateOrUpdateRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            RkmNodeRole[] roles,
            bool immutable,
            IList<RkmNodePlatform> capablePlatforms,
            string architecture,
            CancellationToken cancellationToken)
        {
            var fingerprint = RkmNodeFingerprint.CreateFromPem(attestationIdentityKeyPem);

            object? nodeJson;
            var node = await GetRkmNodeByAttestationIdentityKeyPemAsync(
                attestationIdentityKeyPem,
                cancellationToken);
            if (node == null)
            {
                node = new RkmNode
                {
                    ApiVersion = "rkm.redpoint.games/v1",
                    Kind = "RkmNode",
                    Metadata = new V1ObjectMeta
                    {
                        Name = fingerprint.Substring(0, 8),
                    },
                    Spec = new RkmNodeSpec
                    {
                        NodeName = string.Empty,
                        NodeGroup = string.Empty,
                        Authorized = false,
                        ForceReprovision = false,
                    },
                    Status = new RkmNodeStatus
                    {
                        Roles = null,
                        Immutable = immutable,
                        AttestationIdentityKeyFingerprint = fingerprint,
                        AttestationIdentityKeyPem = attestationIdentityKeyPem,
                        FirstSeen = DateTimeOffset.UtcNow,
                        MostRecentJoinRequest = null,
                        CapablePlatforms = null,
                        Architecture = null,
                        LastSuccessfulProvision = null,
                        Provisioner = null,
                        RegisteredIpAddresses = new(),
                        BootToDisk = false,
                    },
                };
                nodeJson = await _kubernetes.CustomObjects.CreateClusterCustomObjectAsync<JsonElement>(
                    JsonSerializer.SerializeToElement(
                        node,
                        KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNode),
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodes",
                    cancellationToken: cancellationToken);
            }
            else
            {
                nodeJson = await _kubernetes.CustomObjects.PatchClusterCustomObjectAsync<JsonElement>(
                    JsonSerializer.SerializeToElement(
                        new PatchRkmNodePartialUpdate
                        {
                            Status = new PatchRkmNodeStatusPartialUpdate
                            {
                                Roles = roles,
                                Immutable = immutable,
                                AttestationIdentityKeyFingerprint = fingerprint,
                                AttestationIdentityKeyPem = attestationIdentityKeyPem,
                                FirstSeen = node?.Status?.FirstSeen ?? DateTimeOffset.UtcNow,
                                MostRecentJoinRequest = DateTimeOffset.UtcNow,
                                CapablePlatforms = [.. capablePlatforms],
                                Architecture = architecture,
                            }
                        },
                        KubernetesRkmJsonSerializerContext.WithStringEnum.PatchRkmNodePartialUpdate),
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodes",
                    node!.Metadata.Name,
                    cancellationToken: cancellationToken);
            }

            if (nodeJson == null)
            {
                throw new InvalidOperationException("Unable to create RkmNode!");
            }
            return JsonSerializer.Deserialize(
                (JsonElement)nodeJson,
                KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNode)!;
        }

        public async Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyPemAsync(
            string attestationIdentityKeyPem,
            CancellationToken cancellationToken)
        {
            var fingerprint = RkmNodeFingerprint.CreateFromPem(attestationIdentityKeyPem);

            try
            {
                var nodeJson = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync<JsonElement>(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodes",
                    fingerprint.Substring(0, 8),
                    cancellationToken);
                var node = JsonSerializer.Deserialize(
                    (JsonElement)nodeJson,
                    KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNode);
                if (node == null ||
                    node?.Status?.AttestationIdentityKeyFingerprint != fingerprint ||
                    node?.Status?.AttestationIdentityKeyPem != attestationIdentityKeyPem)
                {
                    return null;
                }
                return node;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<RkmNode?> GetRkmNodeByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(attestationIdentityKeyFingerprint);

            try
            {
                var nodeJson = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync<JsonElement>(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodes",
                    attestationIdentityKeyFingerprint.Substring(0, 8),
                    cancellationToken);
                var node = JsonSerializer.Deserialize(
                    (JsonElement)nodeJson,
                    KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNode);
                if (node == null ||
                    node?.Status?.AttestationIdentityKeyFingerprint != attestationIdentityKeyFingerprint)
                {
                    return null;
                }
                return node;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<RkmNode?> GetRkmNodeByRegisteredIpAddressAsync(string registeredIpAddress, CancellationToken cancellationToken)
        {
            // We can't use a field selector here, since field selectors for CRDs can't
            // access arrays.
            var nodesJson = await _kubernetes.CustomObjects.ListClusterCustomObjectAsync<JsonElement>(
                "rkm.redpoint.games",
                "v1",
                "rkmnodes",
                cancellationToken: cancellationToken);
            var nodes = JsonSerializer.Deserialize(
                (JsonElement)nodesJson,
                KubernetesRkmJsonSerializerContext.WithStringEnum.KubernetesListRkmNode);
            if (nodes == null)
            {
                return null;
            }
            var eligible = new List<(RkmNode? node, DateTimeOffset expiresAt)>();
            foreach (var node in nodes)
            {
                var ipAddresses = node?.Status?.RegisteredIpAddresses ?? [];
                foreach (var ipAddress in ipAddresses)
                {
                    if (ipAddress?.Address == registeredIpAddress &&
                        ipAddress?.ExpiresAt > DateTimeOffset.UtcNow)
                    {
                        eligible.Add((node, ipAddress.ExpiresAt ?? DateTimeOffset.MaxValue));
                        return node;
                    }
                }
            }
            return eligible
                .OrderByDescending(x => x.expiresAt)
                .FirstOrDefault().node;
        }

        public async Task UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            RkmNodeStatus status,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(attestationIdentityKeyFingerprint);

            await _kubernetes.CustomObjects.PatchClusterCustomObjectAsync<JsonElement>(
                JsonSerializer.Serialize(
                    new PatchRkmNodeFullStatus
                    {
                        Status = status,
                    },
                    KubernetesRkmJsonSerializerContext.WithStringEnum.PatchRkmNodeFullStatus),
                "rkm.redpoint.games",
                "v1",
                "rkmnodes",
                attestationIdentityKeyFingerprint.Substring(0, 8),
                cancellationToken: cancellationToken);
        }

        public async Task UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(
            string attestationIdentityKeyFingerprint,
            bool forceReprovision,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(attestationIdentityKeyFingerprint);

            await _kubernetes.CustomObjects.PatchClusterCustomObjectAsync<JsonElement>(
                JsonSerializer.Serialize(
                    new PatchRkmNodeForceReprovision
                    {
                        Spec = new PatchRkmNodeSpecForceReprovision
                        {
                            ForceReprovision = forceReprovision,
                        }
                    },
                    KubernetesRkmJsonSerializerContext.WithStringEnum.PatchRkmNodeForceReprovision),
                "rkm.redpoint.games",
                "v1",
                "rkmnodes",
                attestationIdentityKeyFingerprint.Substring(0, 8),
                cancellationToken: cancellationToken);
        }

        public async Task<RkmNodeProvisioner?> GetRkmNodeProvisionerAsync(
            string name,
            JsonTypeInfo<RkmNodeProvisioner> jsonTypeInfoWithSerializerForSteps,
            CancellationToken cancellationToken)
        {
            try
            {
                var nodeProvisioner = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync<JsonElement>(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodeprovisioners",
                    name,
                    cancellationToken);
                return JsonSerializer.Deserialize(
                    (JsonElement)nodeProvisioner,
                    jsonTypeInfoWithSerializerForSteps)!;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<RkmConfiguration> GetRkmConfigurationAsync(CancellationToken cancellationToken)
        {
            object? configuration = null;
            try
            {
                configuration = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync<JsonElement>(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmconfigurations",
                    "default",
                    cancellationToken);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
            }
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
                KubernetesRkmJsonSerializerContext.WithStringEnum.RkmConfiguration)!;
        }

        public async Task ReplaceRkmConfigurationAsync(RkmConfiguration configuration, CancellationToken cancellationToken)
        {
            await _kubernetes.CustomObjects.ReplaceClusterCustomObjectAsync<JsonElement>(
                JsonSerializer.SerializeToElement(
                    configuration,
                    KubernetesRkmJsonSerializerContext.WithStringEnum.RkmConfiguration),
                "rkm.redpoint.games",
                "v1",
                "rkmconfigurations",
                "default",
                cancellationToken: cancellationToken);
        }

        public async Task<RkmNodeGroup?> GetRkmNodeGroupAsync(string name, CancellationToken cancellationToken)
        {
            try
            {
                var nodeProvisioner = await _kubernetes.CustomObjects.GetClusterCustomObjectAsync<JsonElement>(
                    "rkm.redpoint.games",
                    "v1",
                    "rkmnodegroups",
                    name,
                    cancellationToken);
                return JsonSerializer.Deserialize(
                    (JsonElement)nodeProvisioner,
                    KubernetesRkmJsonSerializerContext.WithStringEnum.RkmNodeGroup)!;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
