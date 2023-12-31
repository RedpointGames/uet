﻿namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using Csi.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Protocol;
    using System.Text.Json;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    internal class NodeService : Node.NodeBase
    {
        private readonly ILogger<NodeService> _logger;
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly UefsClient _client;

        public NodeService(
            ILogger<NodeService> logger,
            IRetryableGrpc retryableGrpc,
            UefsClient client)
        {
            _logger = logger;
            _retryableGrpc = retryableGrpc;
            _client = client;
        }

        public override Task<NodeGetInfoResponse> NodeGetInfo(NodeGetInfoRequest request, ServerCallContext context)
        {
            return Task.FromResult(new NodeGetInfoResponse
            {
                NodeId = Environment.MachineName.ToLowerInvariant(),
            });
        }

        public override Task<NodeGetCapabilitiesResponse> NodeGetCapabilities(NodeGetCapabilitiesRequest request, ServerCallContext context)
        {
            return Task.FromResult(new NodeGetCapabilitiesResponse
            {
                // We don't have any additional capabilities.
            });
        }

        public override async Task<NodePublishVolumeResponse> NodePublishVolume(
            NodePublishVolumeRequest request,
            ServerCallContext context)
        {
            try
            {
                if (request.VolumeContext.ContainsKey("tag"))
                {
                    if (!request.VolumeContext.ContainsKey("pullSecretPropertyName"))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Mounting a UEFS package by tag requires a 'pullSecretPropertyName' attribute to be provided, which indicates the property name inside the secret attached via 'nodePublishSecretRef' that contains the Docker registry configuration secret."));
                    }

                    if (!request.Secrets.ContainsKey(request.VolumeContext["pullSecretPropertyName"]))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{request.VolumeContext["pullSecretPropertyName"]}' was specified as the property name for the attached secret, but the attached secret only had these property names available: {string.Join(" ", request.Secrets.Keys.Select(x => $"\"{x}\""))}"));
                    }

                    var tag = request.VolumeContext["tag"];
                    var targetDomain = tag.Substring(0, tag.IndexOf('/'));

                    _logger.LogInformation($"Kubernetes is requesting mount of tag '{tag}' to '{request.TargetPath}' with volume ID '{request.VolumeId}'");

                    var pullSecret = JsonSerializer.Deserialize(
                        request.Secrets[request.VolumeContext["pullSecretPropertyName"]],
                        KubernetesJsonSerializerContext.Default.KubernetesDockerConfig);
                    var pullSecretForTag = pullSecret!.Auths.Where(x =>
                    {
                        if (x.Key.StartsWith("https://"))
                        {
                            return x.Key.Substring(8).StartsWith(targetDomain);
                        }
                        return false;
                    }).Select(x => x.Value).FirstOrDefault();
                    if (pullSecretForTag == null)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"The provided secret for the UEFS mount must have an authentication entry for 'https://{targetDomain}' but it did not."));
                    }

                    string? mountId = null;
                    await foreach (var entry in _retryableGrpc.RetryableStreamingGrpcAsync(
                        _client.MountPackageTag,
                        new MountPackageTagRequest
                        {
                            MountRequest = new MountRequest
                            {
                                OverrideId = $"kube:{request.VolumeId}",
                                MountPath = request.TargetPath,
                                StartupBehaviour = StartupBehaviour.None,
                                WriteScratchPersistence = WriteScratchPersistence.DiscardOnUnmount,
                                WriteScratchPath = string.Empty,
                                TrackPid = 0,
                            },
                            Tag = tag,
                            Credential = new RegistryCredential
                            {
                                Username = pullSecretForTag.Username,
                                Password = pullSecretForTag.Password,
                            }
                        },
                        new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromSeconds(60) },
                        context.CancellationToken))
                    {
                        if (!string.IsNullOrWhiteSpace(entry.PollingResponse.Err))
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Failed to mount package: {entry.PollingResponse.Err}"));
                        }
                        if (entry.PollingResponse.Complete)
                        {
                            mountId = entry.MountId;
                        }
                    }
                    if (mountId == null)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Failed to mount package; UEFS did not assign a mount ID."));
                    }

                    return new NodePublishVolumeResponse();
                }
                else if (
                    request.VolumeContext.ContainsKey("gitUrl") &&
                    request.VolumeContext.ContainsKey("gitCommit"))
                {
                    if (!request.VolumeContext.ContainsKey("gitSecretPrivatePropertyName"))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Mounting a Git commit requires a 'gitSecretPrivatePropertyName' attribute to be provided, which indicates the property name inside the secret attached via 'nodePublishSecretRef' that contains the Git private key in PEM format."));
                    }
                    if (!request.VolumeContext.ContainsKey("gitSecretPublicPropertyName"))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Mounting a Git commit requires a 'gitSecretPublicPropertyName' attribute to be provided, which indicates the property name inside the secret attached via 'nodePublishSecretRef' that contains the Git public key in PEM format."));
                    }

                    if (!request.Secrets.ContainsKey(request.VolumeContext["gitSecretPrivatePropertyName"]))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{request.VolumeContext["gitSecretPrivatePropertyName"]}' was specified as a property name for the attached secret, but the attached secret only had these property names available: {string.Join(" ", request.Secrets.Keys.Select(x => $"\"{x}\""))}"));
                    }
                    if (!request.Secrets.ContainsKey(request.VolumeContext["gitSecretPublicPropertyName"]))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{request.VolumeContext["gitSecretPublicPropertyName"]}' was specified as a property name for the attached secret, but the attached secret only had these property names available: {string.Join(" ", request.Secrets.Keys.Select(x => $"\"{x}\""))}"));
                    }

                    _logger.LogInformation($"Kubernetes is requesting mount of Git commit '{request.VolumeContext["gitCommit"]}' from '{request.VolumeContext["gitUrl"]}' to '{request.TargetPath}' with volume ID '{request.VolumeId}'");

                    _logger.LogInformation($"Fetching Git commit '{request.VolumeContext["gitCommit"]}' from '{request.VolumeContext["gitUrl"]}'...");

                    string? mountId = null;
                    await foreach (var entry in _retryableGrpc.RetryableStreamingGrpcAsync(
                        _client.MountGitCommit,
                        new MountGitCommitRequest
                        {
                            MountRequest = new MountRequest
                            {
                                OverrideId = $"kube:{request.VolumeId}",
                                MountPath = request.TargetPath,
                                StartupBehaviour = StartupBehaviour.None,
                                WriteScratchPersistence = WriteScratchPersistence.DiscardOnUnmount,
                                WriteScratchPath = request.VolumeContext.ContainsKey("scratchPath") ? request.VolumeContext["scratchPath"] ?? string.Empty : string.Empty,
                                TrackPid = 0,
                            },
                            Commit = request.VolumeContext["gitCommit"],
                            Url = request.VolumeContext["gitUrl"],
                            Credential = new GitCredential
                            {
                                SshPublicKeyAsPem = request.Secrets[request.VolumeContext["gitSecretPublicPropertyName"]],
                                SshPrivateKeyAsPem = request.Secrets[request.VolumeContext["gitSecretPrivatePropertyName"]],
                            }
                        },
                        new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromSeconds(60) },
                        context.CancellationToken))
                    {
                        if (!string.IsNullOrWhiteSpace(entry.PollingResponse.Err))
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Failed to mount package: {entry.PollingResponse.Err}"));
                        }
                        if (entry.PollingResponse.Complete)
                        {
                            mountId = entry.MountId;
                        }
                        else
                        {
                            _logger.LogInformation($"Fetching Git commit '{request.VolumeContext["gitCommit"]}' from '{request.VolumeContext["gitUrl"]}': status: '{entry.PollingResponse.Status}', received objects: {entry.PollingResponse.GitReceivedObjects}, indexed objects: {entry.PollingResponse.GitIndexedObjects}, total objects: {entry.PollingResponse.GitTotalObjects}, received bytes: {entry.PollingResponse.GitReceivedBytes}, type: {(entry.PollingResponse.GitSlowFetch ? "libgit2" : "native")}, git server progress message: '{entry.PollingResponse.GitServerProgressMessage}'");
                            await Task.Delay(1000);
                        }
                    }
                    if (mountId == null)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Failed to mount package; UEFS did not assign a mount ID."));
                    }

                    return new NodePublishVolumeResponse();
                }
                else
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Mounting a UEFS package requires either the 'tag' parameter or the 'gitUrl' and 'gitCommit' parameters."));
                }
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(Status.DefaultCancelled);
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message + "\n" + ex.StackTrace));
            }
        }

        public override async Task<NodeUnpublishVolumeResponse> NodeUnpublishVolume(NodeUnpublishVolumeRequest request, ServerCallContext context)
        {
            try
            {
                await _retryableGrpc.RetryableGrpcAsync(
                    _client.UnmountAsync,
                    new UnmountRequest
                    {
                        MountId = $"kube:{request.VolumeId}"
                    },
                    new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromSeconds(60) },
                    CancellationToken.None);
                return new NodeUnpublishVolumeResponse();
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(Status.DefaultCancelled);
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message + "\n" + ex.StackTrace));
            }
        }
    }
}
