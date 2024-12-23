namespace UET.Commands.Internal.RemoteZfsServer
{
    using Grpc.Core;
    using JetBrains.Annotations;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Workspace.RemoteZfs;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Web;

    internal sealed class RemoteZfsServerCommand
    {
        public sealed class Options
        {
        }

        public static Command CreateRemoteZfsServerCommand()
        {
            var options = new Options();
            var command = new Command("remote-zfs-server");
            command.AddAllOptions(options);
            command.AddCommonHandler<RemoteZfsServerCommandInstance>(options);
            return command;
        }

        private sealed class RemoteZfsServerCommandInstance : RemoteZfs.RemoteZfsBase, ICommandInstance
        {
            private readonly ILogger<RemoteZfsServerCommandInstance> _logger;
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private RemoteZfsServerConfig? _config;

            public RemoteZfsServerCommandInstance(
                ILogger<RemoteZfsServerCommandInstance> logger,
                IGrpcPipeFactory grpcPipeFactory)
            {
                _logger = logger;
                _grpcPipeFactory = grpcPipeFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var configJson = Environment.GetEnvironmentVariable("REMOTE_ZFS_SERVER_CONFIG");
                var configJsonPath = Environment.GetEnvironmentVariable("REMOTE_ZFS_SERVER_CONFIG_PATH");
                if (string.IsNullOrEmpty(configJson) && string.IsNullOrEmpty(configJsonPath))
                {
                    _logger.LogError("Expected the REMOTE_ZFS_SERVER_CONFIG or REMOTE_ZFS_SERVER_CONFIG_PATH environment variable to be set.");
                    return 1;
                }

                if (!string.IsNullOrEmpty(configJsonPath))
                {
                    configJson = File.ReadAllText(configJsonPath);
                }

                _config = JsonSerializer.Deserialize(configJson!, RemoteZfsSerializerContext.Default.RemoteZfsServerConfig);
                if (_config == null)
                {
                    _logger.LogError("Expected the remote ZFS server config to be valid.");
                    return 1;
                }

                try
                {
                    await using (_grpcPipeFactory.CreateNetworkServer(this, networkPort: _config.ServerPort ?? 9000)
                        .AsAsyncDisposable(out var server)
                        .ConfigureAwait(false))
                    {
                        await server.StartAsync().ConfigureAwait(false);

                        _logger.LogInformation($"Remote ZFS snapshot service has started on port {server.NetworkPort}.");

                        while (!context.GetCancellationToken().IsCancellationRequested)
                        {
                            await Task.Delay(1000, context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Remote ZFS snapshot service has stopped.");
                }

                return 0;
            }

            public async override Task AcquireWorkspace(
                AcquireWorkspaceRequest request,
                IServerStreamWriter<AcquireWorkspaceResponse> responseStream,
                ServerCallContext context)
            {
                if (_config == null)
                {
                    throw new InvalidOperationException();
                }

                _logger.LogInformation($"Obtained workspace request for template {request.TemplateId}.");

                var template = _config.Templates.FirstOrDefault(x => x.TemplateId == request.TemplateId);
                if (template == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "no such template exists."));
                }

                _logger.LogInformation($"Looking for latest ZFS snapshot for dataset '{template.ZfsSnapshotDataset}'.");

                using var httpClient = new HttpClient(new HttpClientHandler
                {
                    // Ignore TLS errors because TrueNAS is usually running with a self-signed certificate on the local network.
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                });
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _config.TrueNasApiKey);

                var latestSnapshotResponse = await httpClient.SendAsync(
                    new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        Content = new StringContent(
                            JsonSerializer.Serialize(
                                new TrueNasQuery
                                {
                                    QueryFilters = new[]
                                    {
                                        new[] { "dataset", "=", template.ZfsSnapshotDataset },
                                    },
                                    QueryOptions = new TrueNasQueryOptions
                                    {
                                        Extra = new Dictionary<string, string> { { "properties", "name, createtxg" } },
                                        OrderBy = new[] { "-createtxg" },
                                        Limit = 1,
                                    }
                                },
                                RemoteZfsSerializerContext.Default.TrueNasQuery),
                            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")),
                        RequestUri = new Uri($"{_config.TrueNasUrl}/zfs/snapshot"),
                    },
                    cancellationToken: context.CancellationToken).ConfigureAwait(false);
                latestSnapshotResponse.EnsureSuccessStatusCode();

                var latestSnapshot = await latestSnapshotResponse.Content
                    .ReadFromJsonAsync(RemoteZfsSerializerContext.Default.TrueNasSnapshotArray)
                    .ConfigureAwait(false);

                if (latestSnapshot == null || latestSnapshot.Length != 1)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "the target dataset has no latest snapshot."));
                }

                var rzfsTimestamp = $"RZFS_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var rzfsId = $"{template.LinuxParentDataset}/{rzfsTimestamp}";

                _logger.LogInformation($"Cloning ZFS snapshot to dataset '{rzfsId}'.");

                var cloneResponse = await httpClient.SendAsync(
                    new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        Content = new StringContent(
                            JsonSerializer.Serialize(
                                new TrueNasSnapshotClone
                                {
                                    Snapshot = latestSnapshot[0].Id,
                                    DatasetDest = rzfsId,
                                },
                                RemoteZfsSerializerContext.Default.TrueNasSnapshotClone),
                            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")),
                        RequestUri = new Uri($"{_config.TrueNasUrl}/zfs/snapshot/clone"),
                    },
                    cancellationToken: CancellationToken.None /* so this operation can't be interrupted after it succeeds */).ConfigureAwait(false);
                cloneResponse.EnsureSuccessStatusCode();

                try
                {
                    await responseStream.WriteAsync(new AcquireWorkspaceResponse
                    {
                        WindowsShareRemotePath = $"{template.WindowsNetworkShareParentPath}\\{rzfsTimestamp}",
                    }, context.CancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Waiting for client to close connection...");
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, context.CancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                _logger.LogInformation($"Deleting ZFS dataset '{rzfsId}'...");

                var deleteResponse = await httpClient.SendAsync(
                    new HttpRequestMessage
                    {
                        Method = HttpMethod.Delete,
                        RequestUri = new Uri($"{_config.TrueNasUrl}/pool/dataset/id/{HttpUtility.UrlEncode(rzfsId)}?force=true"),
                    },
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                deleteResponse.EnsureSuccessStatusCode();

                _logger.LogInformation($"ZFS request complete.");
            }
        }
    }
}
