namespace Redpoint.KubernetesManager.Components
{
    using k8s.KubeConfigModels;
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifest.Client;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Kestrel;
    using Redpoint.KubernetesManager.Services.Wsl;
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
    using System.Web;

    /// <summary>
    /// This component connects to the controller manifest API, and provides manifests for the locally running kubelet and containerd services.
    /// </summary>
    internal class NodeManifestServerComponent : AbstractHttpListenerComponent
    {
        private readonly ILogger<NodeManifestServerComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IWslTranslation _wslTranslation;
        private readonly IGenericManifestClient _genericManifestClient;
        private readonly List<Func<Task>> _manifestNotifications;
        private readonly Gate _initialManifestReceived;

        private ContainerdManifest? _currentContainerdManifest;
        private KubeletManifest? _currentKubeletManifest;
        private string? _currentStaticPodYaml;
        private CancellationTokenSource? _runCts;
        private Task? _runTask;

        public NodeManifestServerComponent(
            ILogger<NodeManifestServerComponent> logger,
            IServiceProvider serviceProvider,
            IPathProvider pathProvider,
            ILocalEthernetInfo localEthernetInfo,
            IWslTranslation wslTranslation,
            IGenericManifestClient genericManifestClient) : base(
                logger,
                serviceProvider)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _localEthernetInfo = localEthernetInfo;
            _wslTranslation = wslTranslation;
            _genericManifestClient = genericManifestClient;
            _manifestNotifications = new List<Func<Task>>();
            _initialManifestReceived = new Gate();
        }

        protected override string ServerDescription => "local node manifest server";

        protected override IPAddress ListeningAddress => IPAddress.Loopback;

        protected override int ListeningPort => 8375;

        protected override int? SecureListeningPort => null;

        protected override bool IsControllerOnly => false;

        protected override async Task HandleIncomingRequestAsync(HttpContext context, CancellationToken cancellationToken)
        {
            if (context.Request.Path == "/containerd" &&
                context.WebSockets.IsWebSocketRequest)
            {
                await HandleContainerdWebSocketAsync(context, cancellationToken);
            }
            else if (
                context.Request.Path == "/kubelet" &&
                context.WebSockets.IsWebSocketRequest)
            {
                await HandleKubeletWebSocketAsync(context, cancellationToken);
            }
            else if (context.Request.Path == "/kubelet-static-pods")
            {
                using (var writer = new StreamWriter(context.Response.Body, Encoding.UTF8, leaveOpen: true))
                {
                    _logger.LogInformation("Sending static pods manifest for kubelet...");
                    await writer.WriteAsync(_currentStaticPodYaml!);
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }

        protected override async Task OnStartingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait until we know our node name.
            // @todo: This needs to change to get this data from the manifest controller endpoint.
            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            var nodeName = nodeNameContext.NodeName;
            var role = context.Role == RoleType.Controller ? "controller" : "worker";

            // Figure out the endpoint for the manifest.
            string apiServerAddress;
            if (context.Role == RoleType.Controller)
            {
                apiServerAddress = (await _wslTranslation.GetTranslatedIPAddress(cancellationToken)).ToString();
            }
            else
            {
                var nodeContext = await context.WaitForFlagAsync<NodeContextData>(WellKnownFlags.NodeContextAvailable);
                apiServerAddress = nodeContext.ControllerAddress.ToString();
            }

            // Start the generic manifest client.
            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "cache"));
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(async () => await _genericManifestClient.RegisterAndRunWithManifestAsync(
                new Uri($"ws://{apiServerAddress}:8374/node-manifest?nodeName={HttpUtility.UrlEncode(nodeName)}&role={HttpUtility.UrlEncode(role)}"),
                null,
                Path.Combine(_pathProvider.RKMRoot, "cache", "rkm-node-manifest.json"),
                ManifestJsonSerializerContext.Default.NodeManifest,
                async (manifest, cancellationToken) =>
                {
                    _logger.LogInformation("Updating downstream manifests and static pod YAML from new manifest...");

                    // Recompute our manifests and static pod YAML.
                    _currentContainerdManifest = new ContainerdManifest
                    {
                        ManifestVersion = ContainerdManifest.ManifestCurrentVersion,
                        ContainerdInstallRootPath = Path.Combine(_pathProvider.RKMRoot, "containerd"),
                        ContainerdStatePath = Path.Combine(_pathProvider.RKMRoot, "containerd-state"),
                        ContainerdVersion = manifest.ContainerdVersion,
                        UseRedpointContainerd = false,
                        RuncVersion = manifest.RuncVersion,
                        ContainerdEndpointPath = OperatingSystem.IsWindows()
                            ? @"\\.\pipe\containerd-containerd"
                            : Path.Combine(_pathProvider.RKMRoot, "containerd-state", "containerd.sock"),
                        CniPluginsVersion = manifest.CniPluginsVersion,
                        FlannelCniVersionSuffix = manifest.FlannelCniVersionSuffix,
                        FlannelVersion = manifest.FlannelVersion,
                        CniPluginsSymlinkPath = Path.Combine(_pathProvider.RKMRoot, "cni-plugins"),
                    };
                    _currentKubeletManifest = new KubeletManifest
                    {
                        ManifestVersion = KubeletManifest.ManifestCurrentVersion,
                        KubeletInstallRootPath = Path.Combine(_pathProvider.RKMRoot, "kubelet"),
                        KubeletStatePath = Path.Combine(_pathProvider.RKMRoot, "kubelet-state"),
                        KubernetesVersion = manifest.KubernetesVersion,
                        ClusterDomain = manifest.ClusterDnsDomain,
                        ClusterDns = manifest.ClusterDnsServerIpAddress,
                        ContainerdEndpoint = OperatingSystem.IsWindows()
                            ? "npipe://./pipe/containerd-containerd"
                            : $"unix://{Path.Combine(_pathProvider.RKMRoot, "containerd-state", "containerd.sock")}",
                        CaCertData = manifest.CertificateAuthorityPem,
                        NodeCertData = manifest.NodeCertificatePem,
                        NodeKeyData = manifest.NodePrivateKeyPem,
                        ApiServerAddress = apiServerAddress,
                        EtcdVersion = ComponentVersions.Etcd,
                    };
                    _currentStaticPodYaml = manifest.StaticPodsTemplateYaml
                        .Replace("{RKM_ROOT}", _pathProvider.RKMRoot, StringComparison.Ordinal)
                        .Replace("{LOCAL_IP_ADDRESS}", _localEthernetInfo.IPAddress.ToString(), StringComparison.Ordinal);

                    // Notify all listeners.
                    _logger.LogInformation("Notifying connected sockets of new manifests...");
                    foreach (var notifyWebSocket in _manifestNotifications)
                    {
                        try
                        {
                            await notifyWebSocket();
                        }
                        catch { }
                    }

                    // If we are blocked on receiving the initial manifest, we can now proceed.
                    if (!_initialManifestReceived.Opened)
                    {
                        _logger.LogInformation("Permitting startup to proceed now that manifests are ready.");
                        _initialManifestReceived.Open();
                    }
                },
                _runCts.Token), _runCts.Token);

            // Wait until the initial manifest is received before we start serving traffic to the Kubelet
            // and containerd services.
            _logger.LogInformation("Waiting for initial manifest to be received...");
            await _initialManifestReceived.WaitAsync(_runCts.Token);

            _logger.LogInformation("Now starting node manifest server component as initial node manifest has been processed.");
        }

        protected override async Task OnCleanupAsync()
        {
            if (_runTask != null && _runCts != null)
            {
                _logger.LogInformation($"Stopping node manifest server component polling...");

                _runCts.Cancel();
                try
                {
                    await _runTask;
                }
                catch { }
                _runCts.Dispose();
                _runTask = null;
                _runCts = null;
            }

            await base.OnCleanupAsync();
        }

        private async Task HandleContainerdWebSocketAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync(null);

            var handler = async () =>
            {
                _logger.LogInformation("Sending updated manifest for containerd...");
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentContainerdManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            };
            _manifestNotifications.Add(handler);
            try
            {
                _logger.LogInformation("Sending initial manifest for containerd...");
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentContainerdManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                while (webSocket.State == WebSocketState.Open)
                {
                    var buffer = new byte[1024];
                    await webSocket.ReceiveAsync(buffer, cancellationToken);
                }
            }
            finally
            {
                _manifestNotifications.Remove(handler);
            }
        }

        private async Task HandleKubeletWebSocketAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync(null);

            var handler = async () =>
            {
                _logger.LogInformation("Sending updated manifest for kubelet...");
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentKubeletManifest, ManifestJsonSerializerContext.Default.KubeletManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            };
            _manifestNotifications.Add(handler);
            try
            {
                _logger.LogInformation("Sending initial manifest for kubelet...");
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentKubeletManifest, ManifestJsonSerializerContext.Default.KubeletManifest)),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);

                while (webSocket.State == WebSocketState.Open)
                {
                    var buffer = new byte[1024];
                    await webSocket.ReceiveAsync(buffer, cancellationToken);
                }
            }
            finally
            {
                _manifestNotifications.Remove(handler);
            }
        }
    }
}
