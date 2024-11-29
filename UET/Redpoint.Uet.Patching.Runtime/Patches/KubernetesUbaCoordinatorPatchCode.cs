using EpicGames.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnrealBuildBase;
using System.Reflection;
using Timer = System.Threading.Timer;

#nullable enable

namespace UnrealBuildTool
{
    public static class UBAAgentCoordinatorKubernetesConstructor
    {
        static object Construct(
            ILogger logger,
            UnrealBuildAcceleratorConfig ubaConfig,
            CommandLineArguments? additionalArguments = null)
        {
            return new UBAAgentCoordinatorKubernetes(logger, ubaConfig, additionalArguments);
        }
    }

    public class UnrealBuildAcceleratorKubernetesConfig
    {
        /// <summary>
        /// Namespace to deploy Kubernetes pods into.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "Namespace")]
        [CommandLine("-KubernetesNamespace=")]
        public string? Namespace { get; set; }

        /// <summary>
        /// Context from the .kubeconfig file to use.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "Context")]
        [CommandLine("-KubernetesContext=")]
        public string? Context { get; set; }

        /// <summary>
        /// IP address of the network share to store the UBA agent on. This is used to transfer the UBA agent to containers.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "SmbServer")]
        [CommandLine("-KubernetesSmbServer=")]
        public string? SmbServer { get; set; }

        /// <summary>
        /// Share name to use to store the UBA agent.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "SmbShare")]
        [CommandLine("-KubernetesSmbShare=")]
        public string? SmbShare { get; set; }

        /// <summary>
        /// Username to use to connect to network share.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "SmbUsername")]
        [CommandLine("-KubernetesSmbUsername=")]
        public string? SmbUsername { get; set; }

        /// <summary>
        /// Password to use to connect to network share.
        /// </summary>
        [XmlConfigFile(Category = "Kubernetes", Name = "SmbPassword")]
        [CommandLine("-KubernetesSmbPassword=")]
        public string? SmbPassword { get; set; }
    }

    class UBAAgentCoordinatorKubernetes : IUBAAgentCoordinator, IDisposable
    {
        private readonly ILogger _logger;
        private readonly UnrealBuildAcceleratorConfig _ubaConfig;
        private readonly UnrealBuildAcceleratorKubernetesConfig _ubaKubeConfig;
        private readonly System.Type _realCoordinatorType;

        private readonly ConstructorInfo _realCoordinatorConstructor;
        private readonly Dictionary<string, PropertyInfo> _realCoordinatorProperties;
        private readonly Dictionary<string, MethodInfo> _realCoordinatorMethods;

        private UBAExecutor? _executor;
        private IDisposable? _realCoordinator;
        private Timer? _timer;
        private const int _timerPeriod = 5000;
        private bool _forcedStop = false;
        private int _quickRestartsRemaining = 10;

        public UBAAgentCoordinatorKubernetes(
            ILogger logger,
            UnrealBuildAcceleratorConfig ubaConfig,
            CommandLineArguments? additionalArguments = null)
        {
            _logger = logger;

            _logger.LogInformation("Kubernetes UBA: Initializing configuration");
            _ubaConfig = ubaConfig;
            _ubaKubeConfig = new UnrealBuildAcceleratorKubernetesConfig();
            XmlConfig.ApplyTo(_ubaKubeConfig);
            additionalArguments?.ApplyTo(_ubaKubeConfig);

            _logger.LogInformation("Kubernetes UBA: Initializing real coordinator type");
            _realCoordinatorType = System.Runtime.Loader.AssemblyLoadContext.Default.Assemblies
                .First(x => x.GetName()?.Name == "Redpoint.Uet.Patching.Runtime")
                .GetType("Redpoint.Uet.Patching.Runtime.Kubernetes.KubernetesUbaCoordinator")!;
            if (_realCoordinatorType == null)
            {
                throw new InvalidOperationException($"Unable to find type KubernetesUbaCoordinator!");
            }

            _realCoordinatorConstructor = _realCoordinatorType.GetConstructors().FirstOrDefault()!;
            if (_realCoordinatorConstructor == null)
            {
                throw new InvalidOperationException($"Unable to find constructor in KubernetesUbaCoordinator!");
            }

            _realCoordinatorProperties = new Dictionary<string, PropertyInfo>();
            _realCoordinatorMethods = new Dictionary<string, MethodInfo>();
            foreach (var property in new string[]
            {
                "CancellationTokenSource",
                "ClientIsAvailable",
            })
            {
                var foundProperty = _realCoordinatorType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
                if (foundProperty == null)
                {
                    throw new InvalidOperationException($"Unable to find property '{property}' in KubernetesUbaCoordinator!");
                }
                _realCoordinatorProperties.Add(property, foundProperty);
            }
            foreach (var method in new string[]
            {
                "GetAllocatedBlocks",
                "CopyAgentFileToShare",
                "ConnectToClusterAsync",
                "CleanupFinishedKubernetesPodsGloballyAsync",
                "CleanupFinishedKubernetesPodsLocallyAsync",
                "SynchroniseKubernetesNodesAsync",
                "GetMaximumBlockSize",
                "TryAllocateKubernetesNodeAsync",
                "CloseAsync",
            })
            {
                var foundMethod = _realCoordinatorType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
                if (foundMethod == null)
                {
                    throw new InvalidOperationException($"Unable to find method '{method}' in KubernetesUbaCoordinator!");
                }
                _realCoordinatorMethods.Add(method, foundMethod);
            }
        }

        public DirectoryReference? GetUBARootDir()
        {
            return null;
        }

        public void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        public void AddUbaClient(string host, int port)
        {
            _executor!.Server!.AddClient(host, port);
        }

        private CancellationTokenSource? CancellationTokenSource
        {
            get
            {
                if (_realCoordinator != null)
                {
                    return (CancellationTokenSource?)_realCoordinatorProperties["CancellationTokenSource"].GetValue(_realCoordinator);
                }
                else
                {
                    return null;
                }
            }
        }

        public async Task InitAsync(UBAExecutor executor)
        {
            try
            {
                _logger.LogInformation("Kubernetes UBA: InitAsync");

                if (_ubaConfig.bDisableRemote)
                {
                    _logger.LogInformation("Kubernetes UBA: Remoting is disabled, skipping initialization.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_ubaKubeConfig.Namespace))
                {
                    _logger.LogWarning("Kubernetes UBA: Missing Kubernetes -> Namespace in BuildConfiguration.xml.");
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

                _logger.LogInformation("Kubernetes UBA: Constructing real coordinator");
                if (_realCoordinator != null)
                {
                    _realCoordinator.Dispose();
                }
                _realCoordinator = (IDisposable)_realCoordinatorConstructor.Invoke(new object[] { this })!;

                _logger.LogInformation("Kubernetes UBA: Starting initialization");
                // Copy the UbaAgent file to the network share.
                _logger.LogInformation("Kubernetes UBA: (Proxy) Copying file to network share...");
                var ubaDir = DirectoryReference.Combine(Unreal.EngineDirectory, "Binaries", "Win64", "UnrealBuildAccelerator", RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
                var ubaFile = FileReference.Combine(ubaDir, "UbaAgent.exe");
                var agentHash = (await (new FileHasher()).GetDigestAsync(ubaFile)).ToString();
                _realCoordinatorMethods["CopyAgentFileToShare"].Invoke(_realCoordinator, new object[] { ubaFile.FullName, agentHash });

                // Set up Kubernetes client and ensure we can connect to the cluster.
                _logger.LogInformation("Kubernetes UBA: (Proxy) Connecting to cluster...");
                await (Task)_realCoordinatorMethods["ConnectToClusterAsync"].Invoke(_realCoordinator, new object[0])!;

                // Track the executor so we can add clients to it.
                _logger.LogInformation("Kubernetes UBA: Tracking executor...");
                _executor = executor;

                _logger.LogInformation("Kubernetes UBA: Ready to run!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kubernetes UBA: InitAsync failed: {ex}");
            }
        }

        public void Start(ImmediateActionQueue queue, Func<LinkedAction, bool> canRunRemotely)
        {
            _logger.LogInformation("Kubernetes UBA: Start");

            _timer = new Timer(async _ =>
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);

                var stopping = false;
                var quickRestart = false;
                try
                {
                    // If we're cancelled, stop.
                    if (_forcedStop || (CancellationTokenSource != null && CancellationTokenSource.IsCancellationRequested))
                    {
                        _logger.LogInformation("Kubernetes UBA: Loop: Cancelled");
                        stopping = true;
                        return;
                    }

                    // If the client is unavailable, keep looping unless we're done.
                    if (_realCoordinator == null ||
                        !(bool)_realCoordinatorProperties["ClientIsAvailable"].GetValue(_realCoordinator)!)
                    {
                        if (!queue.IsDone)
                        {
                            _logger.LogInformation("Kubernetes UBA: Loop: Client not available (waiting)");
                            if (_quickRestartsRemaining > 0)
                            {
                                quickRestart = true;
                                _quickRestartsRemaining--;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Kubernetes UBA: Loop: Client not available (done)");
                            stopping = true;
                        }
                        return;
                    }

                    // Remove any Kubernetes pods that are complete, or failed to start.
                    await (Task)_realCoordinatorMethods["CleanupFinishedKubernetesPodsGloballyAsync"].Invoke(_realCoordinator, new object[0])!;

                    // If we're done, return.
                    if (queue.IsDone)
                    {
                        _logger.LogInformation("Kubernetes UBA: Loop: Done");
                        stopping = true;
                        return;
                    }

                    // Synchronise Kubernetes nodes with our known node list.
                    await (Task)_realCoordinatorMethods["SynchroniseKubernetesNodesAsync"].Invoke(_realCoordinator, new object[0])!;

                    // Determine the threshold over local allocation.
                    double desiredCpusThreshold = _ubaConfig.bForceBuildAllRemote ? 0 : 5;

                    // Allocate cores from Kubernetes until we're satisified.
                    while (true)
                    {
                        // Check how many additional cores we need to allocate from the cluster.
                        double desiredCpus = queue.EnumerateReadyToCompileActions().Where(x => canRunRemotely(x)).Sum(x => x.Weight);
                        desiredCpus -= (int)_realCoordinatorMethods["GetAllocatedBlocks"].Invoke(_realCoordinator, new object[0])!;
                        if (desiredCpus <= desiredCpusThreshold)
                        {
                            _logger.LogInformation($"Kubernetes UBA: Loop: Skipping (desired CPU {desiredCpus} <= {desiredCpusThreshold})");
                            break;
                        }

                        // Remove any Kubernetes pods that are complete, or failed to start.
                        await (Task)_realCoordinatorMethods["CleanupFinishedKubernetesPodsLocallyAsync"].Invoke(_realCoordinator, new object[0])!;

                        // Compute the biggest size we can allocate.
                        var blockSize = (int)_realCoordinatorMethods["GetMaximumBlockSize"].Invoke(_realCoordinator, new object[] { desiredCpus })!;
                        if (blockSize == 0)
                        {
                            break;
                        }

                        // Try to allocate a node from Kubernetes.
                        var shouldContinue = await (Task<bool>)_realCoordinatorMethods["TryAllocateKubernetesNodeAsync"].Invoke(_realCoordinator, new object[] { blockSize })!;
                        if (!shouldContinue)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException ex) when (CancellationTokenSource != null && ex.CancellationToken == CancellationTokenSource.Token)
                {
                    // Expected exception.
                    stopping = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception in Kubernetes loop: {ex}");
                }
                finally
                {
                    if (!stopping)
                    {
                        if (quickRestart)
                        {
                            _timer?.Change(_timerPeriod, Timeout.Infinite);
                        }
                        else
                        {
                            _timer?.Change(500, Timeout.Infinite);
                        }
                    }
                }
            }, null, 0, _timerPeriod);
        }

        public void Stop()
        {
            CancellationTokenSource?.Cancel();
            _forcedStop = true;
        }

        public async Task CloseAsync()
        {
            CancellationTokenSource?.Cancel();
            _forcedStop = true;

            if (_realCoordinator != null)
            {
                await (Task)_realCoordinatorMethods["CloseAsync"].Invoke(_realCoordinator, new object[0])!;
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
                CancellationTokenSource?.Dispose();
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}