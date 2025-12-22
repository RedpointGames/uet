namespace Redpoint.KubernetesManager.ControllerApi
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Versions;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class GetNodeManifestControllerEndpoint : IControllerEndpoint
    {
        private readonly ILogger<GetNodeManifestControllerEndpoint> _logger;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ICertificateManager _certificateManager;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IPathProvider _pathProvider;

        public GetNodeManifestControllerEndpoint(
            ILogger<GetNodeManifestControllerEndpoint> logger,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ICertificateManager certificateManager,
            ILocalEthernetInfo localEthernetInfo,
            IPathProvider pathProvider)
        {
            _logger = logger;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _certificateManager = certificateManager;
            _localEthernetInfo = localEthernetInfo;
            _pathProvider = pathProvider;
        }

        public string Path => "/node-manifest";

        public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            // @todo: This needs to come from the RkmNode resources in the API server.
            var remoteAddress = context.Connection.RemoteIpAddress;
            var nodeName = context.Request.Query["nodeName"];
            var role = context.Request.Query["role"];
            if (string.IsNullOrWhiteSpace(nodeName) ||
                string.IsNullOrWhiteSpace(role) ||
                (role != "worker" && role != "controller"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            var isController = role == "controller";

            var webSocket = await context.WebSockets.AcceptWebSocketAsync(null);

            await SendManifestToNodeAsync(
                webSocket!,
                remoteAddress,
                nodeName!,
                isController,
                true,
                cancellationToken);

            while (webSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[1024];
                await webSocket.ReceiveAsync(buffer, cancellationToken);
            }
        }

        private async Task SendManifestToNodeAsync(
            WebSocket webSocket,
            IPAddress remoteAddress,
            string nodeName,
            bool isController,
            bool initial,
            CancellationToken cancellationToken)
        {
            var nodeCertificate = await _certificateManager.GenerateCertificateForAuthorizedNodeAsync(
                nodeName,
                // @todo: Can we simplify this to just "remoteAddress"? Or maybe change the
                // condition to "is loopback"?
                isController ? _localEthernetInfo.IPAddress : remoteAddress);

            var certificateAuthorityPem = await File.ReadAllTextAsync(
                System.IO.Path.Combine(_pathProvider.RKMRoot, "certs", "ca", "ca.pem"),
                cancellationToken);

            var nodeManifest = new NodeManifest
            {
                ManifestVersion = NodeManifest.ManifestCurrentVersion,
                StaticPodsTemplateYaml = GetStaticPodsTemplateYaml(isController),
                ContainerdVersion = ComponentVersions.Containerd,
                KubernetesVersion = ComponentVersions.Kubernetes,
                RuncVersion = ComponentVersions.Runc,
                CniPluginsVersion = ComponentVersions.CniPlugins,
                FlannelVersion = ComponentVersions.Flannel,
                FlannelCniVersionSuffix = ComponentVersions.FlannelCniSuffix,
                ClusterDnsDomain = _clusterNetworkingConfiguration.ClusterDNSDomain,
                ClusterDnsServerIpAddress = _clusterNetworkingConfiguration.ClusterDNSServiceIP,
                CertificateAuthorityPem = certificateAuthorityPem,
                NodeCertificatePem = nodeCertificate.CertificatePem,
                NodePrivateKeyPem = nodeCertificate.PrivateKeyPem,
            };

            _logger.LogInformation($"Sending {(initial ? "initial" : "updated")} manifest for node '{nodeName}' at {remoteAddress}...");
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                    nodeManifest,
                    ManifestJsonSerializerContext.Default.NodeManifest)),
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }

        private string GetStaticPodsTemplateYaml(bool isController)
        {
            if (isController)
            {
                // Controller nodes should run etcd, the API server, controller manager, and scheduler.
                return
                $$"""
                apiVersion: v1
                kind: PodList
                items:
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: etcd
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: etcd
                      image: quay.io/coreos/etcd:v{{ComponentVersions.Etcd}}
                      command:
                        - "etcd"
                        - "--name"
                        - "kubernetes"
                        - "--cert-file=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--key-file=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--peer-cert-file=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--peer-key-file=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--trusted-ca-file=/rkm/certs/ca/ca.pem"
                        - "--peer-trusted-ca-file=/rkm/certs/ca/ca.pem"
                        - "--peer-client-cert-auth"
                        - "--client-cert-auth"
                        - "--listen-client-urls"
                        - "https://{LOCAL_IP_ADDRESS}:2379,https://127.0.0.1:2379"
                        - "--advertise-client-urls"
                        - "https://{LOCAL_IP_ADDRESS}:2379"
                        - "--data-dir=/rkm/etcd/data"
                      volumeMounts:
                        - mountPath: /rkm
                          name: rkm
                    volumes:
                    - name: rkm
                      hostPath:
                        type: DirectoryOrCreate
                        path: "{RKM_ROOT}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-apiserver
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-apiserver
                      image: registry.k8s.io/kube-apiserver:v{{ComponentVersions.Kubernetes}}
                      command:
                        - "kube-apiserver"
                        - "--advertise-address={LOCAL_IP_ADDRESS}"
                        - "--allow-privileged=true"
                        - "--audit-log-maxage=30"
                        - "--audit-log-maxbackup=3"
                        - "--audit-log-maxsize=100"
                        - "--audit-log-path=/rkm/logs/audit.log"
                        - "--authorization-mode=Node,RBAC"
                        - "--bind-address=0.0.0.0"
                        - "--client-ca-file=/rkm/certs/ca/ca.pem"
                        - "--enable-admission-plugins=NamespaceLifecycle,NodeRestriction,LimitRanger,ServiceAccount,DefaultStorageClass,ResourceQuota"
                        - "--etcd-cafile=/rkm/certs/ca/ca.pem"
                        - "--etcd-certfile=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--etcd-keyfile=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--etcd-servers=https://{LOCAL_IP_ADDRESS}:2379"
                        - "--event-ttl=1h"
                        - "--encryption-provider-config=/rkm/secrets/encryption-config.yaml"
                        - "--kubelet-certificate-authority=/rkm/certs/ca/ca.pem"
                        - "--kubelet-client-certificate=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--kubelet-client-key=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--runtime-config=api/all=true"
                        - "--service-account-key-file=/rkm/certs/svc/svc-service-account.pem"
                        - "--service-account-signing-key-file=/rkm/certs/svc/svc-service-account.key"
                        - "--service-account-issuer=https://{LOCAL_IP_ADDRESS}:6443"
                        - "--service-cluster-ip-range={{_clusterNetworkingConfiguration.ServiceCIDR}}"
                        - "--service-node-port-range=30000-32767"
                        - "--tls-cert-file=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--tls-private-key-file=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--v=2"
                      volumeMounts:
                        - mountPath: /rkm
                          name: rkm
                    volumes:
                    - name: rkm
                      hostPath:
                        type: DirectoryOrCreate
                        path: "{RKM_ROOT}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-controller-manager
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-controller-manager
                      image: registry.k8s.io/kube-controller-manager:v{{ComponentVersions.Kubernetes}}
                      command:
                        - "kube-controller-manager"
                        - "--bind-address=0.0.0.0"
                        - "--cluster-cidr={{_clusterNetworkingConfiguration.ClusterCIDR}}"
                        - "--cluster-name=kubernetes"
                        - "--cluster-signing-cert-file=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--cluster-signing-key-file=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--kubeconfig=/rkm/kubeconfigs/components/component-kube-controller-manager.kubeconfig"
                        - "--root-ca-file=/rkm/certs/ca/ca.pem"
                        - "--service-account-private-key-file=/rkm/certs/svc/svc-service-account.key"
                        - "--service-cluster-ip-range={{_clusterNetworkingConfiguration.ServiceCIDR}}"
                        - "--use-service-account-credentials=true"
                        - "--allocate-node-cidrs=true"
                        - "--v=2"
                      volumeMounts:
                        - mountPath: /rkm
                          name: rkm
                    volumes:
                    - name: rkm
                      hostPath:
                        type: DirectoryOrCreate
                        path: "{RKM_ROOT}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-scheduler
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-scheduler
                      image: registry.k8s.io/kube-scheduler:v{{ComponentVersions.Kubernetes}}
                      command:
                        - "kube-scheduler"
                        - "--kubeconfig=/rkm/kubeconfigs/components/component-kube-scheduler.kubeconfig"
                        - "--v=2"
                      volumeMounts:
                        - mountPath: /rkm
                          name: rkm
                    volumes:
                    - name: rkm
                      hostPath:
                        type: DirectoryOrCreate
                        path: "{RKM_ROOT}"
                """;
            }
            else
            {
                // Worker nodes do not run static pods at this time.
                return
                $"""
                apiVersion: v1
                kind: PodList
                items: []
                """;
            }
        }
    }
}
