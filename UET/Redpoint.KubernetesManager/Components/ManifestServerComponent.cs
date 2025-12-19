namespace Redpoint.KubernetesManager.Components
{
    using k8s.KubeConfigModels;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
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

    internal class ManifestServerComponent : IComponent, IAsyncDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _apiTask;
        private readonly ILogger<ManifestServerComponent> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IPathProvider _pathProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ICertificateManager _certificateManager;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly List<Func<Task>> _manifestNotifications;
        private ContainerdManifest? _currentContainerdManifest;
        private KubeletManifest? _currentKubeletManifest;

        public ManifestServerComponent(
            ILogger<ManifestServerComponent> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IPathProvider pathProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ICertificateManager certificateManager,
            IKubeConfigManager kubeConfigManager,
            ILocalEthernetInfo localEthernetInfo)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _pathProvider = pathProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _certificateManager = certificateManager;
            _kubeConfigManager = kubeConfigManager;
            _localEthernetInfo = localEthernetInfo;
            _manifestNotifications = new List<Func<Task>>();
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
        }

        private async Task HandleContainerdWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var webSocket = await context.AcceptWebSocketAsync(null);

            var handler = async () =>
            {
                _logger.LogInformation("Sending updated manifest for containerd...");
                await webSocket.WebSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentContainerdManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            };
            _manifestNotifications.Add(handler);
            try
            {
                _logger.LogInformation("Sending initial manifest for containerd...");
                await webSocket.WebSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentContainerdManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                while (webSocket.WebSocket.State == WebSocketState.Open)
                {
                    var buffer = new byte[1024];
                    await webSocket.WebSocket.ReceiveAsync(buffer, cancellationToken);
                }
            }
            finally
            {
                _manifestNotifications.Remove(handler);
            }
        }

        private async Task HandleKubeletWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var webSocket = await context.AcceptWebSocketAsync(null);

            var handler = async () =>
            {
                _logger.LogInformation("Sending updated manifest for kubelet...");
                await webSocket.WebSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentKubeletManifest, ManifestJsonSerializerContext.Default.KubeletManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            };
            _manifestNotifications.Add(handler);
            try
            {
                _logger.LogInformation("Sending initial manifest for kubelet...");
                await webSocket.WebSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentKubeletManifest, ManifestJsonSerializerContext.Default.KubeletManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                while (webSocket.WebSocket.State == WebSocketState.Open)
                {
                    var buffer = new byte[1024];
                    await webSocket.WebSocket.ReceiveAsync(buffer, cancellationToken);
                }
            }
            finally
            {
                _manifestNotifications.Remove(handler);
            }
        }

        private string GetStaticPodsYaml(IContext context)
        {
            if (_currentKubeletManifest == null)
            {
                // No manifest available for some reason.
                return
                $"""
                apiVersion: v1
                kind: PodList
                items: []
                """;
            }

            if (context.Role == RoleType.Controller)
            {
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
                      image: quay.io/coreos/etcd:v{{_currentKubeletManifest.EtcdVersion}}
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
                        - "https://{{_localEthernetInfo.IPAddress}}:2379,https://127.0.0.1:2379"
                        - "--advertise-client-urls"
                        - "https://{{_localEthernetInfo.IPAddress}}:2379"
                        - "--data-dir=/rkm/etcd/data"
                      volumeMounts:
                        - mountPath: /rkm
                          name: rkm
                    volumes:
                    - name: rkm
                      hostPath:
                        type: DirectoryOrCreate
                        path: "{{_pathProvider.RKMRoot}}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-apiserver
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-apiserver
                      image: registry.k8s.io/kube-apiserver:v{{_currentKubeletManifest.KubernetesVersion}}
                      command:
                        - "kube-apiserver"
                        - "--advertise-address={{_localEthernetInfo.IPAddress}}"
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
                        - "--etcd-servers=https://{{_localEthernetInfo.IPAddress}}:2379"
                        - "--event-ttl=1h"
                        - "--encryption-provider-config=/rkm/secrets/encryption-config.yaml"
                        - "--kubelet-certificate-authority=/rkm/certs/ca/ca.pem"
                        - "--kubelet-client-certificate=/rkm/certs/cluster/cluster-kubernetes.pem"
                        - "--kubelet-client-key=/rkm/certs/cluster/cluster-kubernetes.key"
                        - "--runtime-config=api/all=true"
                        - "--service-account-key-file=/rkm/certs/svc/svc-service-account.pem"
                        - "--service-account-signing-key-file=/rkm/certs/svc/svc-service-account.key"
                        - "--service-account-issuer=https://{{_localEthernetInfo.IPAddress}}:6443"
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
                        path: "{{_pathProvider.RKMRoot}}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-controller-manager
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-controller-manager
                      image: registry.k8s.io/kube-controller-manager:v{{_currentKubeletManifest.KubernetesVersion}}
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
                        path: "{{_pathProvider.RKMRoot}}"
                - apiVersion: v1
                  kind: Pod
                  metadata:
                    name: kube-scheduler
                    namespace: kube-system
                  spec:
                    hostNetwork: true
                    containers:
                    - name: kube-scheduler
                      image: registry.k8s.io/kube-scheduler:v{{_currentKubeletManifest.KubernetesVersion}}
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
                        path: "{{_pathProvider.RKMRoot}}"
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

        private async Task RunAsync(IContext rkmContext)
        {
            try
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:8375/");
                listener.Start();
                _logger.LogInformation($"Started rkm local manifest server on port 127.0.0.1:8375.");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cts!.Token,
                    _hostApplicationLifetime.ApplicationStopping);

                while (listener.IsListening && !cts.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().AsCancellable(cts.Token);

                    // Handle the request in the background so we can have multiple websockets open at the same time.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (context.Request.Url?.AbsolutePath == "/containerd" &&
                                context.Request.IsWebSocketRequest)
                            {
                                await HandleContainerdWebSocketAsync(context, cts.Token);
                            }
                            else if (context.Request.Url?.AbsolutePath == "/kubelet" &&
                                context.Request.IsWebSocketRequest)
                            {
                                await HandleKubeletWebSocketAsync(context, cts.Token);
                            }
                            else if (context.Request.Url?.AbsolutePath == "/kubelet-static-pods")
                            {
                                using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true))
                                {
                                    _logger.LogInformation("Sending static pods manifest for kubelet...");
                                    await writer.WriteAsync(GetStaticPodsYaml(rkmContext));
                                }
                                context.Response.Close();
                            }
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested)
                        {
                            // Expected.
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                            catch { }
                            _logger.LogError(ex, $"Failed to respond to a containerd manifest request: {ex.Message}");
                        }
                        finally
                        {
                            context.Response.OutputStream.Close();
                        }
                    }, cts.Token);
                }
            }
            catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested || (_cts?.IsCancellationRequested ?? false))
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"rkm local manifest server loop unexpectedly failed, which will cause rkm to shutdown as it will no longer be able to respond to new nodes: {ex.Message}");
            }
            finally
            {
                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                    !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested &&
                    !(_cts?.IsCancellationRequested ?? false))
                {
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                }
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            if (context.Role == RoleType.Controller)
            {
                await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);
                await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
                await context.WaitForFlagAsync(WellKnownFlags.EncryptionConfigReady);
            }

            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            var nodeName = nodeNameContext.NodeName;

            _currentContainerdManifest = new ContainerdManifest
            {
                ManifestVersion = ContainerdManifest.ManifestCurrentVersion,
                ContainerdInstallRootPath = Path.Combine(_pathProvider.RKMRoot, "containerd"),
                ContainerdStatePath = Path.Combine(_pathProvider.RKMRoot, "containerd-state"),
                ContainerdVersion = ComponentVersions.Containerd,
                UseRedpointContainerd = false,
                RuncVersion = ComponentVersions.Runc,
                ContainerdEndpointPath = OperatingSystem.IsWindows()
                    ? @"\\.\pipe\containerd-containerd"
                    : Path.Combine(_pathProvider.RKMRoot, "containerd-state", "containerd.sock"),
                CniPluginsVersion = ComponentVersions.CniPlugins,
                FlannelCniVersionSuffix = ComponentVersions.FlannelCniSuffix,
                FlannelVersion = ComponentVersions.Flannel,
                CniPluginsSymlinkPath = Path.Combine(_pathProvider.RKMRoot, "cni-plugins"),
            };

            _currentKubeletManifest = new KubeletManifest
            {
                ManifestVersion = KubeletManifest.ManifestCurrentVersion,
                KubeletInstallRootPath = Path.Combine(_pathProvider.RKMRoot, "kubelet"),
                KubeletStatePath = Path.Combine(_pathProvider.RKMRoot, "kubelet-state"),
                KubernetesVersion = ComponentVersions.Kubernetes,
                ClusterDomain = _clusterNetworkingConfiguration.ClusterDNSDomain,
                ClusterDns = _clusterNetworkingConfiguration.ClusterDNSServiceIP,
                ContainerdEndpoint = OperatingSystem.IsWindows()
                    ? "npipe://./pipe/containerd-containerd"
                    : $"unix://{Path.Combine(_pathProvider.RKMRoot, "containerd-state", "containerd.sock")}",
                CaCertData = File.ReadAllText(_certificateManager.GetCertificatePemPath("ca", "ca")),
                NodeCertData = File.ReadAllText(_certificateManager.GetCertificatePemPath("nodes", $"node-{nodeName}")),
                NodeKeyData = File.ReadAllText(_certificateManager.GetCertificateKeyPath("nodes", $"node-{nodeName}")),
                KubeConfigData = File.ReadAllText(_kubeConfigManager.GetKubeconfigPath("nodes", $"node-{nodeName}")),
                EtcdVersion = ComponentVersions.Etcd,
            };

            if (_apiTask == null)
            {
                _logger.LogInformation("Starting local manifest server...");

                _cts = new CancellationTokenSource();
                _apiTask = Task.Run(async () => await RunAsync(context), CancellationToken.None);
            }
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation("Stopping local manifest server...");

                _cts.Cancel();
                try
                {
                    await _apiTask;
                }
                catch { }
                _cts.Dispose();
                _apiTask = null;
                _cts = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation("Stopping local manifest server...");

                _cts.Cancel();
                try
                {
                    await _apiTask;
                }
                catch { }
                _cts.Dispose();
                _apiTask = null;
                _cts = null;
            }
        }
    }
}
