namespace UET.Commands.Internal.CMakeUbaServer
{
    using k8s;
    using k8s.Autorest;
    using k8s.Models;
    using System.Net.Sockets;
    using System.Net;
    using System.Runtime.CompilerServices;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uba;
    using Redpoint.Hashing;
    using System.Globalization;
    using UET.Commands.Config;

    internal class UbaCoordinatorKubernetes : IDisposable
    {
        private readonly string _ubaRootPath;
        private readonly ILogger _logger;
        private readonly UbaCoordinatorKubernetesConfig _ubaKubeConfig;
        private CancellationTokenSource? _cancellationSource;
        private string? _id;
        private string? _ubaAgentRemotePath;
        private string? _ubaAgentHash;
        private Kubernetes? _client;
        private Timer? _timer;
        private const int _timerPeriod = 1000;
        private Dictionary<string, KubernetesNodeState> _kubernetesNodes;
        private IUbaServer? _ubaServer;

        private string KubernetesNamespace
        {
            get
            {
                return _ubaKubeConfig?.Namespace ?? "default";
            }
        }

        public UbaCoordinatorKubernetes(
            string ubaRootPath,
            ILogger logger,
            UbaCoordinatorKubernetesConfig ubaKubeConfig)
        {
            _ubaRootPath = ubaRootPath;
            _logger = logger;
            _ubaKubeConfig = ubaKubeConfig;
            _cancellationSource = new CancellationTokenSource();
            _kubernetesNodes = new Dictionary<string, KubernetesNodeState>();
        }

        public async Task InitAsync(IUbaServer ubaServer)
        {
            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.Namespace))
            {
                // If this is not set, then the developer probably wants to use Horde instead.
                return;
            }

            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.Context))
            {
                _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> Context in BuildConfiguration.xml.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.SmbServer))
            {
                _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> SmbServer in BuildConfiguration.xml.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.SmbShare))
            {
                _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> SmbShare in BuildConfiguration.xml.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.SmbUsername))
            {
                _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> SmbUsername in BuildConfiguration.xml.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_ubaKubeConfig.SmbPassword))
            {
                _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> SmbPassword in BuildConfiguration.xml.");
                return;
            }

            _logger.LogInformation("Kubernetes UBA: InitAsync");

            try
            {
                // Set the cancellation source for cancelling the pod.
                _cancellationSource?.Cancel();
                _cancellationSource?.Dispose();
                _cancellationSource = new CancellationTokenSource();

                // Clear out current nodes state.
                _kubernetesNodes.Clear();

                // Generate an ID to identify the jobs we're running.
                _id = Guid.NewGuid().ToString();

                // Copy the UbaAgent file to the UBA root directory.
                var ubaFile = new FileInfo(Path.Combine(_ubaRootPath, "UbaAgent.exe"));
                var agentHash = (await Hash.XxHash64OfFileAsync(ubaFile.FullName, CancellationToken.None).ConfigureAwait(false)).Hash.ToString(CultureInfo.InvariantCulture);
                var ubaRemoteDir = new DirectoryInfo(Path.Combine(@$"\\{_ubaKubeConfig.SmbServer}\{_ubaKubeConfig.SmbShare}\Uba", $"cmake{agentHash}"))!;
                var ubaRemoteFile = new FileInfo(Path.Combine(ubaRemoteDir.FullName, "UbaAgent.exe"));
                try
                {
                    Directory.CreateDirectory(ubaRemoteDir.FullName);
                    File.Copy(ubaFile.FullName, ubaRemoteFile.FullName);
                }
                catch
                {
                    // File already copied.
                }
                _ubaAgentRemotePath = ubaRemoteFile.FullName;
                _ubaAgentHash = agentHash.ToString();

                // Set up Kubernetes client and ensure we can connect to the cluster.
                var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: _ubaKubeConfig.Context);
                _client = new Kubernetes(config);
                await _client.ListNamespacedPodAsync(KubernetesNamespace, cancellationToken: _cancellationSource.Token).ConfigureAwait(false);

                // Track the executor so we can add clients to it.
                _ubaServer = ubaServer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kubernetes UBA: InitAsync failed: {ex.Message}");
            }
        }

        private async Task DeleteServiceAndPodAsync(V1Pod pod, CancellationToken? cancellationToken)
        {
            try
            {
                await _client.DeleteNamespacedServiceAsync(
                    pod.Name(),
                    pod.Namespace(),
                    cancellationToken: cancellationToken ?? _cancellationSource?.Token ?? CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
            }
            try
            {
                await _client.DeleteNamespacedPodAsync(
                    pod.Name(),
                    pod.Namespace(),
                    cancellationToken: cancellationToken ?? _cancellationSource?.Token ?? CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
            }
            if (_kubernetesNodes.TryGetValue(pod.Spec.NodeName, out var node))
            {
                node.AllocatedBlocks.RemoveAll(x => x.KubernetesPod.Name() == pod.Name());
            }
        }

        private async IAsyncEnumerable<V1Pod> EnumerateNodePodAsync(string nodeName, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? continueParameter = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var list = await _client.ListPodForAllNamespacesAsync(
                    fieldSelector: $"spec.nodeName={nodeName}",
                    continueParameter: continueParameter,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                foreach (var item in list.Items)
                {
                    yield return item;
                }
                continueParameter = list.Metadata.ContinueProperty;
            } while (!string.IsNullOrWhiteSpace(continueParameter));
        }

        private async IAsyncEnumerable<V1Pod> EnumerateNamespacedPodAsync(string labelSelector, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? continueParameter = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var list = await _client.ListNamespacedPodAsync(
                    KubernetesNamespace,
                    labelSelector: labelSelector,
                    continueParameter: continueParameter,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                foreach (var item in list.Items)
                {
                    yield return item;
                }
                continueParameter = list.Metadata.ContinueProperty;
            } while (!string.IsNullOrWhiteSpace(continueParameter));
        }

        private async IAsyncEnumerable<V1Service> EnumerateNamespacedServiceAsync(string labelSelector, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? continueParameter = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var list = await _client.ListNamespacedServiceAsync(
                    KubernetesNamespace,
                    labelSelector: labelSelector,
                    continueParameter: continueParameter,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                foreach (var item in list.Items)
                {
                    yield return item;
                }
                continueParameter = list.Metadata.ContinueProperty;
            } while (!string.IsNullOrWhiteSpace(continueParameter));
        }

        private async IAsyncEnumerable<V1Node> EnumerateNodesAsync(string labelSelector, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? continueParameter = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var list = await _client.ListNodeAsync(
                    labelSelector: labelSelector,
                    continueParameter: continueParameter,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                foreach (var item in list.Items)
                {
                    if (item.Spec.Taints != null && (
                        item.Spec.Taints.Any(x => x.Effect == "NoSchedule") ||
                        item.Spec.Taints.Any(x => x.Effect == "NoExecute")))
                    {
                        continue;
                    }
                    yield return item;
                }
                continueParameter = list.Metadata.ContinueProperty;
            } while (!string.IsNullOrWhiteSpace(continueParameter));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "This call is not used for cryptography.")]
        public void Start()
        {
            _logger.LogInformation("Kubernetes UBA: Start");

            _timer = new Timer(async _ =>
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);

                var stopping = false;
                try
                {
                    // If we're cancelled, stop.
                    if (_cancellationSource == null || _cancellationSource.IsCancellationRequested)
                    {
                        _logger.LogInformation("Kubernetes UBA: Loop: Cancelled");
                        stopping = true;
                        return;
                    }

                    // If the client is unavailable, keep looping unless we're done.
                    if (_client == null)
                    {
                        _logger.LogInformation("Kubernetes UBA: Loop: Not configured to use Kubernetes.");
                        return;
                    }

                    // Remove any Kubernetes pods that are complete, or failed to start.
                    await foreach (var pod in EnumerateNamespacedPodAsync("uba=true", _cancellationSource.Token))
                    {
                        if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                        {
                            _logger.LogInformation($"Removing Kubernetes block: {pod.Name()} (cleanup)");
                            await DeleteServiceAndPodAsync(pod, _cancellationSource.Token).ConfigureAwait(false);

                            foreach (var kv in _kubernetesNodes)
                            {
                                kv.Value.AllocatedBlocks.RemoveAll(candidatePod =>
                                    candidatePod.KubernetesPod != null &&
                                    candidatePod.KubernetesPod.Name() == pod.Name());
                            }
                        }
                    }

                    // Synchronise Kubernetes nodes with our known node list.
                    var knownNodeNames = new HashSet<string>();
                    await foreach (var node in EnumerateNodesAsync("kubernetes.io/os=windows", _cancellationSource.Token))
                    {
                        knownNodeNames.Add(node.Name());
                        if (_kubernetesNodes.TryGetValue(node.Name(), out var existingEntry))
                        {
                            existingEntry.KubernetesNode = node;
                        }
                        else
                        {
                            existingEntry = new KubernetesNodeState
                            {
                                NodeId = node.Name(),
                                KubernetesNode = node,
                            };
                            _kubernetesNodes.Add(node.Name(), existingEntry);
                        }
                        var newPodsList = new List<V1Pod>();
                        await foreach (var pod in EnumerateNodePodAsync(node.Name(), _cancellationSource.Token))
                        {
                            newPodsList.Add(pod);
                        }
                        existingEntry.KubernetesPods = newPodsList;
                    }

                    // Determine the threshold over local allocation.
                    double desiredCpusThreshold = 0;

                    // Allocate cores from Kubernetes until we're satisified.
                    while (true)
                    {
                        // Check how many additional cores we need to allocate from the cluster.
                        double desiredCpus = _ubaServer!.ProcessesPendingInQueue;
                        desiredCpus -= _kubernetesNodes.SelectMany(x => x.Value.AllocatedBlocks).Sum(y => y.AllocatedCores);
                        if (desiredCpus <= desiredCpusThreshold)
                        {
                            _logger.LogDebug($"Kubernetes UBA: Loop: Skipping (desired CPU {desiredCpus} <= {desiredCpusThreshold})");
                            break;
                        }

                        // Remove any Kubernetes pods that are complete, or failed to start.
                        await foreach (var pod in EnumerateNamespacedPodAsync($"uba=true,uba.queueId={_id}", _cancellationSource.Token))
                        {
                            if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                            {
                                _logger.LogInformation($"Removing Kubernetes block: {pod.Name()}");
                                await DeleteServiceAndPodAsync(pod, _cancellationSource.Token).ConfigureAwait(false);
                            }
                        }

                        // Compute the biggest size we can allocate.
                        var blockSize = _kubernetesNodes
                            .Select(x => x.Value.CoresAllocatable)
                            .Where(x => x != 0)
                            .DefaultIfEmpty(0)
                            .Max();
                        blockSize = Math.Min(blockSize, (int)Math.Floor(desiredCpus));
                        if (blockSize == 0)
                        {
                            // No more cores to allocate from.
                            break;
                        }

                        // Pick an available node, weighted by the available cores.
                        var selectedNode = _kubernetesNodes
                            .Select(x => x.Value)
                            .SelectMany(x =>
                            {
                                var blocks = new List<KubernetesNodeState>();
                                _logger.LogInformation($"Kubernetes UBA: Loop: Node '{x.NodeId}' has\nCPU: {x.CoresTotal} total, {x.CoresNonUba} non-UBA, {x.CoresAllocated} allocated, {x.CoresAvailable} available, {x.CoresAllocatable} allocatable.\nMemory: {x.MemoryTotal} total, {x.MemoryNonUba} non-UBA, {x.MemoryAllocated} allocated, {x.MemoryAvailable} available.\nBlocks: {x.AllocatedBlocks.Count} allocated.");
                                for (var c = 0; c < x.CoresAllocatable; c += blockSize)
                                {
                                    if (c + blockSize <= x.CoresAllocatable)
                                    {
                                        blocks.Add(x);
                                    }
                                }
                                return blocks;
                            })
                            .OrderBy(_ => Random.Shared.NextInt64())
                            .FirstOrDefault();

                        // If we don't have any available node, break out of the loop node.
                        if (selectedNode == null)
                        {
                            break;
                        }

                        // Generate the next block ID for this node.
                        int highestBlockId = 0;
                        await foreach (var pod in EnumerateNamespacedPodAsync($"uba.nodeId={selectedNode.NodeId}", _cancellationSource.Token))
                        {
                            var thisBlockId = int.Parse(pod.GetLabel("uba.blockId"), CultureInfo.InvariantCulture);
                            highestBlockId = Math.Max(highestBlockId, thisBlockId);
                        }
                        var nextBlockId = highestBlockId + 1;

                        // Create the pod and service.
                        var name = $"uba-{selectedNode.NodeId}-{nextBlockId}";
                        _logger.LogInformation($"Allocating Kubernetes block: {name}");
                        var labels = new Dictionary<string, string>
                        {
                            { "uba", "true" },
                            { "uba.nodeId", selectedNode.NodeId! },
                            { "uba.blockId", nextBlockId.ToString(CultureInfo.InvariantCulture) },
                            { "uba.queueId", _id! },
                        };
                        var kubernetesPod = await _client.CreateNamespacedPodAsync(
                            new V1Pod
                            {
                                Metadata = new V1ObjectMeta
                                {
                                    Name = name,
                                    Labels = labels,
                                },
                                Spec = new V1PodSpec
                                {
                                    AutomountServiceAccountToken = false,
                                    NodeSelector = new Dictionary<string, string>
                                    {
                                        { "kubernetes.io/os", "windows" },
                                    },
                                    RestartPolicy = "Never",
                                    TerminationGracePeriodSeconds = 0,
                                    Volumes = new List<V1Volume>
                                    {
                                        new V1Volume
                                        {
                                            Name = "uba-storage",
                                            HostPath = new V1HostPathVolumeSource
                                            {
                                                Path = @$"C:\Uba\{name}",
                                            }
                                        }
                                    },
                                    Containers = new List<V1Container>
                                    {
                                        new V1Container
                                        {
                                            Image = "mcr.microsoft.com/powershell:lts-windowsservercore-ltsc2022",
                                            ImagePullPolicy = "IfNotPresent",
                                            Name = "uba-agent",
                                            Resources = new V1ResourceRequirements
                                            {
                                                Requests = new Dictionary<string, ResourceQuantity>
                                                {
                                                    { "cpu", new ResourceQuantity(blockSize.ToString(CultureInfo.InvariantCulture)) },
													// @note: We don't set 'memory' here because it can be finicky to get the container to get allocated.
												},
                                                Limits = new Dictionary<string, ResourceQuantity>
                                                {
                                                    { "cpu", new ResourceQuantity(blockSize.ToString(CultureInfo.InvariantCulture)) },
													// @note: We don't set 'memory' here because it can be finicky to get the container to get allocated.
												},
                                            },
                                            SecurityContext = new V1SecurityContext
                                            {
                                                WindowsOptions = new V1WindowsSecurityContextOptions
                                                {
                                                    RunAsUserName = "ContainerAdministrator",
                                                }
                                            },
                                            Command = new List<string>
                                            {
                                                @"C:\Program Files\PowerShell\latest\pwsh.exe",
                                            },
                                            Args = new List<string>
                                            {
                                                "-Command",
                                                $@"Start-Sleep -Seconds 1; Write-Host ""Mapping network drive...""; C:\Windows\system32\net.exe use Z: \\{_ubaKubeConfig.SmbServer}\{_ubaKubeConfig.SmbShare}\Uba /USER:{_ubaKubeConfig.SmbUsername} {_ubaKubeConfig.SmbPassword}; Write-Host ""Copying UBA agent...""; Copy-Item Z:\cmake{_ubaAgentHash}\UbaAgent.exe C:\UbaAgent.exe; Write-Host ""Running UBA agent...""; C:\UbaAgent.exe -Verbose -Listen=7000 -NoPoll -listenTimeout=120 -ProxyPort=7001 -Dir=C:\UbaData -MaxIdle=15 -MaxCpu={blockSize}; Write-Host ""UBA agent exited with exit code: $LastExitCode""; exit $LastExitCode;",
                                            },
                                            Ports = new List<V1ContainerPort>
                                            {
                                                new V1ContainerPort
                                                {
                                                    ContainerPort = 7000,
                                                    Protocol = "TCP",
                                                },
                                                new V1ContainerPort
                                                {
                                                    ContainerPort = 7001,
                                                    Protocol = "TCP",
                                                }
                                            },
                                            VolumeMounts = new List<V1VolumeMount>
                                            {
                                                new V1VolumeMount
                                                {
                                                    Name = "uba-storage",
                                                    MountPath = @"C:\UbaData",
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            KubernetesNamespace,
                            cancellationToken: _cancellationSource.Token).ConfigureAwait(false);
                        V1Service kubernetesService;
                    createService:
                        try
                        {
                            kubernetesService = await _client.CreateNamespacedServiceAsync(
                                new V1Service
                                {
                                    Metadata = new V1ObjectMeta
                                    {
                                        Name = name,
                                        Labels = labels,
                                    },
                                    Spec = new V1ServiceSpec
                                    {
                                        Selector = labels,
                                        Type = "NodePort",
                                        Ports = new List<V1ServicePort>
                                        {
                                            new V1ServicePort
                                            {
                                                Name = "uba",
                                                Port = 7000,
                                                TargetPort = new IntstrIntOrString("7000"),
                                                Protocol = "TCP",
                                            },
                                            new V1ServicePort
                                            {
                                                Name = "uba-proxy",
                                                Port = 7001,
                                                TargetPort = new IntstrIntOrString("7001"),
                                                Protocol = "TCP",
                                            },
                                        },
                                    },
                                },
                                KubernetesNamespace,
                                cancellationToken: _cancellationSource.Token).ConfigureAwait(false);
                        }
                        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
                        {
                            await _client.DeleteNamespacedServiceAsync(name, KubernetesNamespace, cancellationToken: _cancellationSource.Token).ConfigureAwait(false);
                            goto createService;
                        }

                        // Track the worker.
                        var worker = new KubernetesNodeWorker
                        {
                            KubernetesPod = kubernetesPod,
                            KubernetesService = kubernetesService,
                            AllocatedCores = blockSize,
                        };
                        selectedNode.AllocatedBlocks.Add(worker);

                        // In the background, wait for the worker to become ready and allocate it to UBA.
                        _ = Task.Run(async () =>
                        {
                            var didRegister = false;
                            try
                            {
                                // Wait for the service to have node ports.
                                while (worker.UbaHost == null || worker.UbaPort == null)
                                {
                                    _cancellationSource.Token.ThrowIfCancellationRequested();

                                    // Refresh service status.
                                    worker.KubernetesService = await _client.ReadNamespacedServiceAsync(
                                        worker.KubernetesService.Name(),
                                        worker.KubernetesService.Namespace(),
                                        cancellationToken: _cancellationSource.Token).ConfigureAwait(false);

                                    // If a port doesn't have NodePort, it's not allocated yet.
                                    if (worker.KubernetesService.Spec.Ports.Any(x => x.NodePort == null))
                                    {
                                        await Task.Delay(1000).ConfigureAwait(false);
                                        continue;
                                    }

                                    // We should have the node port now.
                                    worker.UbaHost = selectedNode.KubernetesNode!.Status.Addresses
                                        .Where(x => x.Type == "InternalIP")
                                        .Select(x => x.Address)
                                        .First();
                                    worker.UbaPort = worker.KubernetesService.Spec.Ports.First(x => x.Name == "uba").NodePort!.Value;
                                    break;
                                }

                                // Wait for the pod to start.
                                var secondsElapsed = 0;
                                while (worker.KubernetesPod.Status.Phase == "Pending" && secondsElapsed < 30)
                                {
                                    await Task.Delay(1000).ConfigureAwait(false);
                                    worker.KubernetesPod = await _client.ReadNamespacedPodAsync(
                                        worker.KubernetesPod.Name(),
                                        worker.KubernetesPod.Namespace(),
                                        cancellationToken: _cancellationSource.Token).ConfigureAwait(false);
                                    secondsElapsed++;
                                }
                                if (worker.KubernetesPod.Status.Phase == "Pending")
                                {
                                    // Timed out.
                                    _logger.LogWarning($"Kubernetes timed out while allocating: {name}");
                                    return;
                                }

                                // Add the worker to UBA.
                                _logger.LogInformation($"Kubernetes block is ready: {name} ({worker.UbaHost}:{worker.UbaPort.Value})");
                                var didAddAgent = false;
                                for (int i = 0; i < 30; i++)
                                {
                                    if (_ubaServer.AddRemoteAgent(worker.UbaHost, worker.UbaPort.Value))
                                    {
                                        didAddAgent = true;
                                        break;
                                    }
                                    await Task.Delay(1000).ConfigureAwait(false);
                                }
                                if (!didAddAgent)
                                {
                                    _logger.LogError("Unable to register Kubernetes UBA agent with UBA library!");
                                }
                                else
                                {
                                    didRegister = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Exception in Kubernetes wait loop: {ex}");
                            }
                            finally
                            {
                                if (!didRegister)
                                {
                                    // This pod/service could not be allocated. The main loop can then try to allocate again.
                                    await DeleteServiceAndPodAsync(worker.KubernetesPod, _cancellationSource.Token).ConfigureAwait(false);
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException ex) when (_cancellationSource != null && ex.CancellationToken == _cancellationSource.Token)
                {
                    // Expected exception.
                    stopping = true;
                }
                catch (TaskCanceledException)
                {
                    // Expected exception.
                    stopping = true;
                }
                catch (SocketException)
                {
                    _logger.LogWarning("Unable to reach Kubernetes cluster.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception in Kubernetes loop: {ex}");
                }
                finally
                {
                    if (!stopping)
                    {
                        _timer?.Change(_timerPeriod, Timeout.Infinite);
                    }
                }

            }, null, 0, _timerPeriod);
        }

        public void Stop()
        {
            _cancellationSource?.Cancel();
        }

        public async Task CloseAsync()
        {
            _cancellationSource?.Cancel();

            if (_client != null)
            {
                try
                {
                    // Remove any Kubernetes pods that are complete.
                    await foreach (var pod in EnumerateNamespacedPodAsync("uba=true", CancellationToken.None))
                    {
                        if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                        {
                            _logger.LogInformation($"Removing Kubernetes block: {pod.Name()} (cleanup on close)");
                            await DeleteServiceAndPodAsync(pod, CancellationToken.None).ConfigureAwait(false);
                        }
                    }

                    // Remove any Kubernetes pods owned by us.
                    if (!string.IsNullOrWhiteSpace(_id))
                    {
                        await foreach (var pod in EnumerateNamespacedPodAsync($"uba.queueId={_id}", CancellationToken.None))
                        {
                            _logger.LogInformation($"Removing Kubernetes block: {pod.Name()} (unconditional)");
                            await DeleteServiceAndPodAsync(pod, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }
                catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Ignore this transient error on shutdown.
                }
                catch (SocketException)
                {
                    // Ignore this transient error on shutdown.
                }
                catch (HttpRequestException)
                {
                    // Ignore this transient error on shutdown.
                }
            }
        }

        public void Dispose()
        {
            Stop();
            CloseAsync().Wait();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationSource?.Dispose();
                _cancellationSource = null;
                _timer?.Dispose();
                _timer = null;
                _client?.Dispose();
                _client = null;
            }
        }
    }
}
