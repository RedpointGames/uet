using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.KubernetesManager.Services;
using Redpoint.ProcessExecution;
using Redpoint.ServiceControl;
using Redpoint.Windows.HostNetworkingService;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace UET.Commands.Cluster
{
    [SupportedOSPlatform("windows")]
    internal sealed class ClusterAllocateSourceVipCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<ClusterAllocateSourceVipCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command(
                        "allocate-source-vip",
                        "Allocate a source VIP for kube-proxy.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton(_ =>
                    {
                        return IHnsApi.GetInstance();
                    });
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> NetworkName = new Option<string>("--network-name", () => "flannel.4096", "The HNS network name.");
            public Option<string> ContainerId = new Option<string>("--container-id", () => "kube-proxy", "The container ID to pass to the 'host-local' CNI plugin.");
            public Option<DirectoryInfo> CniBinPath = new Option<DirectoryInfo>("--cni-bin-path", "The path to the directory containing the CNI plugins, including 'host-local.exe'.")
            {
                IsRequired = true,
            };
            public Option<FileInfo> SourceVipFilePath = new Option<FileInfo>("--source-vip-file-path", "The path to the file to write out with the source VIP inside it.")
            {
                IsRequired = true,
            };
            public Option<DirectoryInfo> HostLocalDataDir = new Option<DirectoryInfo>("--host-local-data-dir", "The directory that the 'host-local' CNI should store it's state in.")
            {
                IsRequired = true,
            };
            public Option<int?> LivenessProbePort = new Option<int?>("--liveness-probe-port", "An optional port that the 'allocate-source-vip' command should listen on for localhost while it is performing work. This can be set as the liveness probe for the init container to detect if the allocate source VIP command is not running properly.");
        }

        private sealed class ClusterAllocateSourceVipCommandInstance : ICommandInstance
        {
            private readonly IHnsApi _hnsService;
            private readonly ILogger<ClusterAllocateSourceVipCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public ClusterAllocateSourceVipCommandInstance(
                IHnsApi hnsService,
                ILogger<ClusterAllocateSourceVipCommandInstance> logger,
                IProcessExecutor processExecutor,
                Options options)
            {
                _hnsService = hnsService;
                _logger = logger;
                _processExecutor = processExecutor;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var networkName = context.ParseResult.GetValueForOption(_options.NetworkName)!;
                var containerId = context.ParseResult.GetValueForOption(_options.ContainerId)!;
                var cniBinPath = context.ParseResult.GetValueForOption(_options.CniBinPath)!;
                var sourceVipFilePath = context.ParseResult.GetValueForOption(_options.SourceVipFilePath)!;
                var hostLocalDataDir = context.ParseResult.GetValueForOption(_options.HostLocalDataDir)!;
                var livenessProbePort = context.ParseResult.GetValueForOption(_options.LivenessProbePort);

                TcpListener? listener = null;
                using var listenerCancel = CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());
                Task? listenerTask = null;
                if (livenessProbePort.HasValue && livenessProbePort != 0)
                {
                    _logger.LogInformation($"Starting liveness probe listener on port {livenessProbePort.Value}...");

                    listener = new TcpListener(IPAddress.Loopback, livenessProbePort.Value);
                    listener.Start();

                    listenerTask = Task.Run(async () =>
                    {
                        while (!listenerCancel.IsCancellationRequested)
                        {
                            var client = await listener.AcceptTcpClientAsync(listenerCancel.Token);
                            if (client == null)
                            {
                                continue;
                            }

                            _logger.LogInformation($"Responding to liveness probe...");

                            try
                            {
                                using (var writer = new StreamWriter(client.GetStream(), leaveOpen: false))
                                {
                                    await writer.WriteLineAsync("alive");
                                    await writer.FlushAsync(listenerCancel.Token);
                                }
                                client.Close();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Exception when responding to liveness probe: {ex.Message}");
                            }
                        }
                    });
                }

                try
                {
                retryGetNetwork:
                    var newHnsNetwork = _hnsService.GetHnsNetworks().FirstOrDefault(x => x.Name == networkName);
                    if (newHnsNetwork == null)
                    {
                        _logger.LogInformation($"Waiting for HNS network '{networkName}' to appear...");
                        await Task.Delay(1000, context.GetCancellationToken());
                        goto retryGetNetwork;
                    }

                    if (hostLocalDataDir.Exists)
                    {
                        var hostLocalNetworkDataDir = new DirectoryInfo(Path.Combine(hostLocalDataDir.FullName, networkName));
                        if (hostLocalNetworkDataDir.Exists)
                        {
                            foreach (var file in hostLocalNetworkDataDir.GetFiles())
                            {
                                if (file.Name != "lock" && !file.Name.StartsWith("last_reserved_ip", StringComparison.Ordinal))
                                {
                                    var lines = await File.ReadAllLinesAsync(file.FullName, context.GetCancellationToken());
                                    if (lines.Length >= 2)
                                    {
                                        var readContainerId = lines[0];
                                        var ifName = lines[1];

                                        if (readContainerId == containerId && ifName == "source-vip")
                                        {
                                            var sourceVipAddress = file.Name;
                                            _logger.LogInformation($"Read existing source VIP for '{containerId}': {sourceVipAddress}");
                                            if (sourceVipFilePath != null)
                                            {
                                                var sourceVipDirectoryPath = Path.GetDirectoryName(sourceVipFilePath.FullName);
                                                if (sourceVipDirectoryPath != null)
                                                {
                                                    Directory.CreateDirectory(sourceVipDirectoryPath);
                                                }
                                                File.WriteAllText(sourceVipFilePath.FullName, sourceVipAddress);
                                                _logger.LogInformation($"Wrote source VIP file to: {sourceVipFilePath.FullName}");
                                            }
                                            return 0;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInformation($"No source VIP for '{containerId}' reserved; creating one...");

                    var configData =
                        $$"""
                        {
                            "cniVersion": "1.0.0",
                            "name": "{{networkName}}",
                            "ipam": {
                                "type": "host-local",
                                "ranges": [[{"subnet":"{{newHnsNetwork.Subnets[0].AddressPrefix}}"}]],
                                "dataDir": "{{hostLocalDataDir.FullName.Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                            }
                        }
                        """;

                    _logger.LogInformation($"Acquiring source VIP address using 'host-local.exe'...");
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(cniBinPath.FullName, "host-local.exe"),
                            Arguments = [],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "CNI_COMMAND", "ADD" },
                                { "CNI_CONTAINERID", containerId },
                                { "CNI_NETNS", "kube-proxy" },
                                { "CNI_IFNAME", "source-vip" },
                                { "CNI_PATH", cniBinPath.FullName },
                            }
                        },
                        new HostLocalCaptureSpecification(configData, stdout, stderr),
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError(
                            $"""
                            'host-local.exe' returned non-zero exit code:

                            Standard output:
                            {stdout.ToString()}

                            Standard error:
                            {stderr.ToString()}
                            """);
                        return 1;
                    }

                    try
                    {
                        var sourceVipResponse = JsonSerializer.Deserialize(
                            stdout.ToString(),
                            SourceVipResponseJsonSerializerContext.Default.SourceVipResponse);
                        if (sourceVipResponse != null && sourceVipResponse.IPs.Length > 0)
                        {
                            var sourceVipAddress = sourceVipResponse.IPs[0].Address.Split('/')[0];
                            _logger.LogInformation($"Allocated or reused source VIP address: {sourceVipAddress}");
                            if (sourceVipFilePath != null)
                            {
                                var sourceVipDirectoryPath = Path.GetDirectoryName(sourceVipFilePath.FullName);
                                if (sourceVipDirectoryPath != null)
                                {
                                    Directory.CreateDirectory(sourceVipDirectoryPath);
                                }
                                File.WriteAllText(sourceVipFilePath.FullName, sourceVipAddress);
                                _logger.LogInformation($"Wrote source VIP file to: {sourceVipFilePath.FullName}");
                            }
                            return 0;
                        }
                        else
                        {
                            _logger.LogError("No source VIP reserved.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to unmarshal source VIP response: {ex}");
                    }
                    return 1;
                }
                finally
                {
                    if (listener != null)
                    {
                        listener.Stop();
                        listenerCancel.Cancel();
                        try
                        {
                            await listenerTask!;
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        listenerCancel.Cancel();
                    }
                }
            }

            private class HostLocalCaptureSpecification : ICaptureSpecification
            {
                private readonly string? _input;
                private readonly StringBuilder _output;
                private readonly StringBuilder _error;

                public HostLocalCaptureSpecification(
                    string? input,
                    StringBuilder output,
                    StringBuilder error)
                {
                    _input = input;
                    _output = output;
                    _error = error;
                }

                public bool InterceptStandardInput => true;

                public bool InterceptStandardOutput => true;

                public bool InterceptStandardError => true;

                public void OnReceiveStandardError(string data)
                {
                    _error.Append(data);
                }

                public void OnReceiveStandardOutput(string data)
                {
                    _output.Append(data);
                }

                public string? OnRequestStandardInputAtStartup()
                {
                    return _input;
                }
            }
        }
    }
}
