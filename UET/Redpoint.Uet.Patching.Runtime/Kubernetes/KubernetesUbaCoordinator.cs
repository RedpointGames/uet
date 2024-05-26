namespace Redpoint.Uet.Patching.Runtime.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Autorest;
    using k8s.Models;

    internal class KubernetesUbaCoordinator : IDisposable
    {
        private readonly object _ubtHookObject;
        private readonly IKubernetesUbaConfig _ubaKubeConfig;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Dictionary<string, KubernetesNodeState> _kubernetesNodes;
        private readonly string _id;
        private string? _ubaAgentRemotePath;
        private string? _ubaAgentHash;
        private Kubernetes? _client;

        public KubernetesUbaCoordinator(object ubtHookObject)
        {
            _ubtHookObject = ubtHookObject;

            // Create our config from the hook object.
            _ubaKubeConfig = new KubernetesUbaConfigFromHook(ubtHookObject);

            // Set the cancellation source for cancelling the work.
            _cancellationSource = new CancellationTokenSource();

            // Initialize our dictionary that tracks Kubernetes nodes.
            _kubernetesNodes = new Dictionary<string, KubernetesNodeState>();

            // Generate an ID to identify the jobs we're running.
            _id = Guid.NewGuid().ToString();
        }

        private string KubernetesNamespace
        {
            get
            {
                return _ubaKubeConfig?.Namespace ?? "default";
            }
        }

        public CancellationTokenSource CancellationTokenSource => _cancellationSource;

        public bool ClientIsAvailable
        {
            get
            {
                return _client != null;
            }
        }

        private void LogInformation(string message)
        {
            _ubtHookObject.GetType()
                .GetMethod("LogInformation", BindingFlags.Public | BindingFlags.Instance)!
                .Invoke(_ubtHookObject, new object[]
                {
                    message
                });
        }

        private void AddUbaClient(string ubaHost, int ubaPort)
        {
            _ubtHookObject.GetType()
                .GetMethod("AddUbaClient", BindingFlags.Public | BindingFlags.Instance)!
                .Invoke(_ubtHookObject, new object[]
                {
                    ubaHost,
                    ubaPort
                });
        }

        public void CopyAgentFileToShare(string ubaAgentExe, string ubaAgentHash)
        {
            LogInformation("Kubernetes UBA: Copying agent file to network share...");
            var ubaRemoteDir = Path.Combine(@$"\\{_ubaKubeConfig.SmbServer}\{_ubaKubeConfig.SmbShare}\Uba", ubaAgentExe);
            var ubaRemoteFile = Path.Combine(ubaRemoteDir, "UbaAgent.exe");
            try
            {
                Directory.CreateDirectory(ubaRemoteDir);
                File.Copy(ubaAgentExe, ubaRemoteFile);
            }
            catch
            {
                // File already copied.
            }
            _ubaAgentRemotePath = ubaRemoteFile;
            _ubaAgentHash = ubaAgentHash;
            LogInformation("Kubernetes UBA: Copied agent file to network share.");
        }

        public async Task ConnectToClusterAsync()
        {
            LogInformation("Kubernetes UBA: Connecting to cluster...");
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: _ubaKubeConfig.Context);
            _client = new Kubernetes(config);
            await _client.ListNamespacedPodAsync(KubernetesNamespace, cancellationToken: _cancellationSource.Token);
            LogInformation("Kubernetes UBA: Connected to cluster.");
        }

        #region Kubernetes Helpers

        private async Task DeleteServiceAndPodAsync(V1Pod pod, CancellationToken? cancellationToken)
        {
            try
            {
                await _client.DeleteNamespacedServiceAsync(pod.Name(), pod.Namespace(), cancellationToken: cancellationToken ?? _cancellationSource?.Token ?? CancellationToken.None);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
            }
            try
            {
                await _client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), cancellationToken: cancellationToken ?? _cancellationSource?.Token ?? CancellationToken.None);
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
                var list = await _client.ListPodForAllNamespacesAsync(fieldSelector: $"spec.nodeName={nodeName}", continueParameter: continueParameter, cancellationToken: cancellationToken);
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
                var list = await _client.ListNamespacedPodAsync(KubernetesNamespace, labelSelector: labelSelector, continueParameter: continueParameter, cancellationToken: cancellationToken);
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
                var list = await _client.ListNamespacedServiceAsync(KubernetesNamespace, labelSelector: labelSelector, continueParameter: continueParameter, cancellationToken: cancellationToken);
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
                var list = await _client.ListNodeAsync(labelSelector: labelSelector, continueParameter: continueParameter, cancellationToken: cancellationToken);
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

        #endregion

        public async Task CleanupFinishedKubernetesPodsGloballyAsync()
        {
            await foreach (var pod in EnumerateNamespacedPodAsync("uba=true", _cancellationSource.Token))
            {
                if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                {
                    LogInformation($"Removing Kubernetes block: {pod.Name()} (cleanup)");
                    await DeleteServiceAndPodAsync(pod, _cancellationSource.Token);
                }
            }
        }

        public async Task CleanupFinishedKubernetesPodsLocallyAsync()
        {
            await foreach (var pod in EnumerateNamespacedPodAsync($"uba=true,uba.queueId={_id}", _cancellationSource.Token))
            {
                if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                {
                    LogInformation($"Removing Kubernetes block: {pod.Name()}");
                    await DeleteServiceAndPodAsync(pod, _cancellationSource.Token);
                }
            }
        }

        public int GetAllocatedBlocks()
        {
            return _kubernetesNodes.SelectMany(x => x.Value.AllocatedBlocks).Sum(y => y.AllocatedCores);
        }

        public async Task SynchroniseKubernetesNodesAsync()
        {
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
            var currentNodeNames = new HashSet<string>(_kubernetesNodes.Keys);
            foreach (var current in currentNodeNames)
            {
                if (!knownNodeNames.Contains(current))
                {
                    _kubernetesNodes.Remove(current);
                }
            }
        }

        public int GetMaximumBlockSize(double desiredCpus)
        {
            var blockSize = _kubernetesNodes
                .Select(x => x.Value.CoresAllocatable)
                .Where(x => x != 0)
                .DefaultIfEmpty(0)
                .Max();
            blockSize = Math.Min(blockSize, (int)Math.Floor(desiredCpus));
            return blockSize;
        }

        public async Task<bool> TryAllocateKubernetesNodeAsync(int blockSize)
        {
            // Pick an available node, weighted by the available cores.
            var selectedNode = _kubernetesNodes
                .Select(x => x.Value)
                .SelectMany(x =>
                {
                    var blocks = new List<KubernetesNodeState>();
                    LogInformation($"Kubernetes UBA: Loop: Node '{x.NodeId}' has\nCPU: {x.CoresTotal} total, {x.CoresNonUba} non-UBA, {x.CoresAllocated} allocated, {x.CoresAvailable} available, {x.CoresAllocatable} allocatable.\nMemory: {x.MemoryTotal} total, {x.MemoryNonUba} non-UBA, {x.MemoryAllocated} allocated, {x.MemoryAvailable} available.\nBlocks: {x.AllocatedBlocks.Count} allocated.");
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
                return false;
            }

            // Generate the next block ID for this node.
            int highestBlockId = 0;
            await foreach (var pod in EnumerateNamespacedPodAsync($"uba.nodeId={selectedNode.NodeId}", _cancellationSource.Token))
            {
                var thisBlockId = int.Parse(pod.GetLabel("uba.blockId"));
                highestBlockId = Math.Max(highestBlockId, thisBlockId);
            }
            var nextBlockId = highestBlockId + 1;

            // Create the pod and service.
            var name = $"uba-{selectedNode.NodeId}-{nextBlockId}";
            LogInformation($"Allocating Kubernetes block: {name}");
            var labels = new Dictionary<string, string>
            {
                { "uba", "true" },
                { "uba.nodeId", selectedNode.NodeId! },
                { "uba.blockId", nextBlockId.ToString() },
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
                                        { "cpu", new ResourceQuantity(blockSize.ToString()) },
										// @note: We don't set 'memory' here because it can be finicky to get the container to get allocated.
									},
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        { "cpu", new ResourceQuantity(blockSize.ToString()) },
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
                                    $@"Start-Sleep -Seconds 1; Write-Host ""Mapping network drive...""; C:\Windows\system32\net.exe use Z: \\{_ubaKubeConfig.SmbServer}\{_ubaKubeConfig.SmbShare}\Uba /USER:{_ubaKubeConfig.SmbUsername} {_ubaKubeConfig.SmbPassword}; Write-Host ""Copying UBA agent...""; Copy-Item Z:\{_ubaAgentHash}\UbaAgent.exe C:\UbaAgent.exe; Write-Host ""Running UBA agent...""; C:\UbaAgent.exe -Verbose -Listen=7000 -NoPoll -listenTimeout=120 -ProxyPort=7001 -Dir=C:\UbaData -MaxIdle=15 -MaxCpu={blockSize}; Write-Host ""UBA agent exited with exit code: $LastExitCode""; exit $LastExitCode;",
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
                cancellationToken: _cancellationSource.Token);
            var kubernetesService = await _client.CreateNamespacedServiceAsync(
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
                cancellationToken: _cancellationSource.Token);

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
                            cancellationToken: _cancellationSource.Token);

                        // If a port doesn't have NodePort, it's not allocated yet.
                        if (worker.KubernetesService.Spec.Ports.Any(x => x.NodePort == null))
                        {
                            await Task.Delay(1000);
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
                        await Task.Delay(1000);
                        worker.KubernetesPod = await _client.ReadNamespacedPodAsync(
                            worker.KubernetesPod.Name(),
                            worker.KubernetesPod.Namespace(),
                            cancellationToken: _cancellationSource.Token);
                        secondsElapsed++;
                    }
                    if (worker.KubernetesPod.Status.Phase == "Pending")
                    {
                        // Timed out.
                        LogInformation($"Kubernetes timed out while allocating: {name}");
                        return;
                    }

                    // Add the worker to UBA.
                    LogInformation($"Kubernetes block is ready: {name} ({worker.UbaHost}:{worker.UbaPort.Value})");
                    AddUbaClient(worker.UbaHost, worker.UbaPort.Value);
                    didRegister = true;
                }
                catch (Exception ex)
                {
                    LogInformation($"Exception in Kubernetes wait loop: {ex}");
                }
                finally
                {
                    if (!didRegister)
                    {
                        // This pod/service could not be allocated. The main loop can then try to allocate again.
                        await DeleteServiceAndPodAsync(worker.KubernetesPod, _cancellationSource.Token);
                    }
                }
            });
            return true;
        }

        public async Task CloseAsync()
        {
            // Remove any Kubernetes pods that are complete.
            await foreach (var pod in EnumerateNamespacedPodAsync("uba=true", CancellationToken.None))
            {
                if (pod.Status.Phase == "Succeeded" || pod.Status.Phase == "Failed" || pod.Status.Phase == "Unknown")
                {
                    // _logger.LogInformation($"Removing Kubernetes block: {pod.Name()} (cleanup on close)");
                    await DeleteServiceAndPodAsync(pod, CancellationToken.None);
                }
            }

            // Remove any Kubernetes pods owned by us.
            if (!string.IsNullOrWhiteSpace(_id))
            {
                await foreach (var pod in EnumerateNamespacedPodAsync($"uba.queueId={_id}", CancellationToken.None))
                {
                    // _logger.LogInformation($"Removing Kubernetes block: {pod.Name()} (unconditional)");
                    await DeleteServiceAndPodAsync(pod, CancellationToken.None);
                }
            }
        }

        public void Dispose()
        {
            CloseAsync().Wait();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationSource?.Dispose();
            }
        }
    }
}
