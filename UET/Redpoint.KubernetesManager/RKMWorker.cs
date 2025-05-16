using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Redpoint.KubernetesManager.Components;
using Redpoint.KubernetesManager.Services;
using Redpoint.KubernetesManager.Signalling;
using Redpoint.KubernetesManager.Signalling.Data;
using System.Net;

namespace Redpoint.KubernetesManager
{
    internal class RKMWorker : BackgroundService
    {
        private readonly ILogger<RKMWorker> _logger;
        private readonly ILogger<Executor> _executorLogger;
        private readonly IPathProvider _pathProvider;
        private readonly IControllerAutodiscoveryService _controllerAutodiscoveryService;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly INodeManifestClient _nodeManifestClient;
        private readonly IComponent[] _components;
        private readonly RKMCommandLineArguments _commandLine;
        private readonly IProcessKiller _processKiller;

        public RKMWorker(
            ILogger<RKMWorker> logger,
            ILogger<Executor> executorLogger,
            IPathProvider pathProvider,
            IControllerAutodiscoveryService controllerAutodiscoveryService,
            IHostApplicationLifetime hostApplicationLifetime,
            INodeManifestClient nodeManifestClient,
            IComponent[] components,
            RKMCommandLineArguments commandLine,
            IProcessKiller processKiller)
        {
            _logger = logger;
            _executorLogger = executorLogger;
            _pathProvider = pathProvider;
            _controllerAutodiscoveryService = controllerAutodiscoveryService;
            _hostApplicationLifetime = hostApplicationLifetime;
            _nodeManifestClient = nodeManifestClient;
            _components = components;
            _commandLine = commandLine;
            _processKiller = processKiller;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_commandLine.Arguments.Contains("--help"))
            {
                Console.WriteLine("rkm-daemon [options]");
                Console.WriteLine();
                Console.WriteLine("--controller         If this is a new instance, set it up as a controller.");
                Console.WriteLine("--node <address>     If this is a new instance, set it up as a node for the controller at the given address.");
                Environment.ExitCode = 1;
                _hostApplicationLifetime.StopApplication();
                return;
            }

            // Set up the installation directory. RKM uses a unique directory for each
            // install so that if you have to completely replace an RKM install on a
            // machine, there's no chance of earlier installations interfering.
            _pathProvider.EnsureRKMRoot();

            _logger.LogInformation($"Running RKM version: {_pathProvider.RKMVersion}");

            // Use command line arguments, or perform auto-discovery if we don't have an existing role.
            var roleFile = Path.Combine(_pathProvider.RKMRoot, "role");
            if (!File.Exists(roleFile))
            {
                if (_commandLine.Arguments.Contains("--controller"))
                {
                    _logger.LogInformation("Skipping auto-discovery. This instance will be a controller because --controller was passed on the command line.");
                    await File.WriteAllTextAsync(roleFile, $"controller", stoppingToken);
                }
                else if (_commandLine.Arguments.Contains("--node"))
                {
                    _logger.LogInformation("Skipping auto-discovery. This instance will be a node because --node was passed on the command line.");
                    var addressIndex = Array.IndexOf(_commandLine.Arguments, "--node") + 1;
                    var address = _commandLine.Arguments[addressIndex];
                    _logger.LogInformation($"Using the controller address from the command line: {address}");
                    await File.WriteAllTextAsync(roleFile, $"node,{address}", stoppingToken);
                }
                else
                {
                    var existingControllerAddress = await _controllerAutodiscoveryService.AttemptAutodiscoveryOfController(stoppingToken);
                    if (existingControllerAddress != null)
                    {
                        _logger.LogInformation($"Found existing controller at {existingControllerAddress}, this installation will run as a node on the existing cluster.");
                        await File.WriteAllTextAsync(roleFile, $"node,{existingControllerAddress}", stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation($"No existing controller could be found on the network, rkm will set this up as a controller for a new cluster. Press Ctrl-C to cancel this installation within 10 seconds.");
                        await Task.Delay(10 * 1000, stoppingToken);
                        if (stoppingToken.IsCancellationRequested)
                        {
                            return;
                        }
                        await File.WriteAllTextAsync(roleFile, $"controller", stoppingToken);
                    }
                }
            }

            // Determine the role from the role file; the role of an installation
            // never changes once it is set (though you can create a new installation
            // to get a different result).
            var role = await File.ReadAllTextAsync(roleFile, stoppingToken);
            RoleType roleType;
            string? controllerAddress = null;
            if (role == "controller")
            {
                // We are running as a controller. Start the auto-discovery service early.
                _controllerAutodiscoveryService.StartAutodiscovery();
                roleType = RoleType.Controller;
            }
            else
            {
                // We are running as a node to an existing cluster.
                roleType = RoleType.Node;
                controllerAddress = role.Split(",")[1].Trim();
            }

            // Kill any existing processes before we start launching stuff.
            await _processKiller.EnsureProcessesAreNotRunning(stoppingToken);

            // Create the executor and register components.
            var executor = new Executor(
                _executorLogger,
                _hostApplicationLifetime,
                roleType,
                stoppingToken);
            _logger.LogInformation($"Registering {_components.Length} components with executor.");
            executor.RegisterComponents(_components);

            // If this is a node, we need to get the node manifest before we 
            // can start execution, and we'll use that to preemptively set
            // the NodeContextAvailable flag.
            if (role != "controller")
            {
                // Get the manifest from the controller, or load it from disk.
                _logger.LogInformation($"Fetching node manifest from controller or disk...");
                var parsedAddress = IPAddress.Parse(controllerAddress!);
                var nodeManifest = await _nodeManifestClient.ObtainNodeManifestAsync(parsedAddress, Environment.MachineName.ToLowerInvariant(), stoppingToken);
                if (string.IsNullOrWhiteSpace(nodeManifest.CalicoWindowsConfig))
                {
                    _logger.LogCritical("rkm can not continue because the controller did not provide a kubeconfig for calico-windows. Update RKM on the controller first and then try again.");
                    File.Delete(Path.Combine(_pathProvider.RKMRoot, "node-manifest.yaml"));
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                    return;
                }

                // Preemptively set the flag so that the node can start up.
                _logger.LogInformation($"Node manifest is now available for components.");
                executor.SetFlag(WellKnownFlags.NodeContextAvailable, new NodeContextData(nodeManifest, parsedAddress));
            }

            // Now start execution.
            _logger.LogInformation($"Performing preflight checks...");
            await executor.RaiseSignalAsync(WellKnownSignals.PreflightChecks, null, stoppingToken);
            try
            {
                _logger.LogInformation($"Starting RKM components...");
                await executor.RaiseSignalAsync(WellKnownSignals.Started, null, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Stopping RKM components...");
                await executor.RaiseSignalAsync(WellKnownSignals.Stopping, null, CancellationToken.None);
            }
        }
    }
}
