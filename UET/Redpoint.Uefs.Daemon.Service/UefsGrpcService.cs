namespace Redpoint.Uefs.Daemon.Service
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Service.Mounting;
    using Redpoint.Uefs.Daemon.Service.Pulling;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;

    internal class UefsGrpcService : UefsBase
    {
        private readonly ILogger<UefsGrpcService> _logger;
        private readonly IUefsDaemon _daemon;
        private readonly IMounter<MountPackageFileRequest> _packageFileMounter;
        private readonly IMounter<MountPackageTagRequest> _packageTagMounter;
        private readonly IMounter<MountGitCommitRequest> _gitCommitMounter;
        private readonly IMounter<MountGitHubCommitRequest> _gitHubCommitMounter;
        private readonly IMounter<MountFolderSnapshotRequest> _folderSnapshotMounter;
        private readonly IPuller<PullPackageTagRequest> _packageTagPuller;
        private readonly IPuller<PullGitCommitRequest> _gitCommitPuller;

        public UefsGrpcService(
            ILogger<UefsGrpcService> logger,
            IUefsDaemon daemon,
            IMounter<MountPackageFileRequest> packageFileMounter,
            IMounter<MountPackageTagRequest> packageTagMounter,
            IMounter<MountGitCommitRequest> gitCommitMounter,
            IMounter<MountGitHubCommitRequest> gitHubCommitMounter,
            IMounter<MountFolderSnapshotRequest> folderSnapshotMounter,
            IPuller<PullPackageTagRequest> packageTagPuller,
            IPuller<PullGitCommitRequest> gitCommitPuller)
        {
            _logger = logger;
            _daemon = daemon;
            _packageFileMounter = packageFileMounter;
            _packageTagMounter = packageTagMounter;
            _gitCommitMounter = gitCommitMounter;
            _gitHubCommitMounter = gitHubCommitMounter;
            _folderSnapshotMounter = folderSnapshotMounter;
            _packageTagPuller = packageTagPuller;
            _gitCommitPuller = gitCommitPuller;
        }

        private MountContext GetMountContext(MountRequest request)
        {
            Process? trackedPid = null;
            if (request.TrackPid != 0)
            {
                trackedPid = Process.GetProcessById(request.TrackPid);
                if (trackedPid == null || trackedPid.HasExited)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unable to get process handle for PID '{request.TrackPid}'"));
                }
                else
                {
                    _logger.LogInformation($"Tracking process PID {trackedPid.Id} ({trackedPid.ProcessName}) for automatic unmount.");
                }
            }

            string id;
            if (!string.IsNullOrWhiteSpace(request.OverrideId))
            {
                id = request.OverrideId;
                if (_daemon.CurrentMounts.ContainsKey(id))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"There is already another mount with the same ID."));
                }
            }
            else
            {
                id = Guid.NewGuid().ToString();
                while (_daemon.CurrentMounts.ContainsKey(id))
                {
                    id = Guid.NewGuid().ToString();
                }
            }

            return new MountContext
            {
                MountId = id,
                TrackedPid = trackedPid?.Id,
                IsBeingMountedOnStartup = false,
            };
        }

        private async Task RunMountAsync(
            IServerStreamWriter<MountResponse> stream,
            MountContext mountContext,
            Func<TransactionListenerDelegate, Task> operation)
        {
            PollingResponse? lastPollingResponse = null;
            var streamClosed = false;
            try
            {
                await operation(async response =>
                {
                    lastPollingResponse = response;
                    if (!streamClosed)
                    {
                        await stream.WriteAsync(new MountResponse
                        {
                            PollingResponse = response,
                            MountId = response.Complete && string.IsNullOrWhiteSpace(response.Err) ? mountContext.MountId : string.Empty,
                        });
                    }
                });
                if (lastPollingResponse == null)
                {
                    lastPollingResponse = new PollingResponse
                    {
                        Type = PollingResponseType.Immediate,
                    };
                }
                lastPollingResponse.CompleteForMount();
                await stream.WriteAsync(new MountResponse
                {
                    PollingResponse = lastPollingResponse,
                    MountId = mountContext.MountId,
                });
                streamClosed = true;
            }
            catch (Exception ex)
            {
                if (lastPollingResponse == null)
                {
                    lastPollingResponse = new PollingResponse
                    {
                        Type = PollingResponseType.Immediate,
                    };
                }
                lastPollingResponse.Exception(ex);
                try
                {
                    await stream.WriteAsync(new MountResponse
                    {
                        PollingResponse = lastPollingResponse,
                        MountId = string.Empty,
                    });
                }
                catch { }
                _logger.LogError(ex, ex.Message);
            }
        }

        public override async Task MountPackageTag(MountPackageTagRequest request, IServerStreamWriter<MountResponse> responseStream, ServerCallContext context)
        {
            var mountContext = GetMountContext(request.MountRequest);
            await RunMountAsync(
                responseStream,
                mountContext,
                async onPollingResponse =>
                {
                    await _packageTagMounter.MountAsync(
                        _daemon,
                        mountContext,
                        request,
                        onPollingResponse,
                        context.CancellationToken);
                });
        }

        public override async Task MountPackageFile(MountPackageFileRequest request, IServerStreamWriter<MountResponse> responseStream, ServerCallContext context)
        {
            var mountContext = GetMountContext(request.MountRequest);
            await RunMountAsync(
                responseStream,
                mountContext,
                async onPollingResponse =>
                {
                    await _packageFileMounter.MountAsync(
                        _daemon,
                        mountContext,
                        request,
                        onPollingResponse,
                        context.CancellationToken);
                });
        }

        public override async Task MountGitCommit(MountGitCommitRequest request, IServerStreamWriter<MountResponse> responseStream, ServerCallContext context)
        {
            var mountContext = GetMountContext(request.MountRequest);
            await RunMountAsync(
                responseStream,
                mountContext,
                async onPollingResponse =>
                {
                    await _gitCommitMounter.MountAsync(
                        _daemon,
                        mountContext,
                        request,
                        onPollingResponse,
                        context.CancellationToken);
                });
        }

        public override async Task MountGitHubCommit(MountGitHubCommitRequest request, IServerStreamWriter<MountResponse> responseStream, ServerCallContext context)
        {
            var mountContext = GetMountContext(request.MountRequest);
            await RunMountAsync(
                responseStream,
                mountContext,
                async onPollingResponse =>
                {
                    await _gitHubCommitMounter.MountAsync(
                        _daemon,
                        mountContext,
                        request,
                        onPollingResponse,
                        context.CancellationToken);
                });
        }

        public override async Task MountFolderSnapshot(MountFolderSnapshotRequest request, IServerStreamWriter<MountResponse> responseStream, ServerCallContext context)
        {
            var mountContext = GetMountContext(request.MountRequest);
            await RunMountAsync(
                responseStream,
                mountContext,
                async onPollingResponse =>
                {
                    await _folderSnapshotMounter.MountAsync(
                        _daemon,
                        mountContext,
                        request,
                        onPollingResponse,
                        context.CancellationToken);
                });
        }

        private async Task RunPullAsync(
            IServerStreamWriter<PullResponse> stream,
            Func<TransactionListenerDelegate, Action<string>, Task> operation)
        {
            string? transactionId = null;
            PollingResponse? lastPollingResponse = null;
            var streamClosed = false;
            try
            {
                await operation(
                    async response =>
                    {
                        lastPollingResponse = response;
                        if (!streamClosed)
                        {
                            await stream.WriteAsync(new PullResponse
                            {
                                PollingResponse = response,
                                OperationId = transactionId ?? string.Empty,
                            });
                        }
                    },
                    t =>
                    {
                        transactionId = t;
                    });
                if (lastPollingResponse == null || !lastPollingResponse.Complete)
                {
                    throw new InvalidOperationException();
                }
                await stream.WriteAsync(new PullResponse
                {
                    PollingResponse = lastPollingResponse,
                    OperationId = transactionId ?? string.Empty,
                });
                streamClosed = true;
            }
            catch (Exception ex)
            {
                if (lastPollingResponse == null)
                {
                    lastPollingResponse = new PollingResponse
                    {
                        Type = PollingResponseType.Immediate,
                    };
                }
                lastPollingResponse.Exception(ex);
                try
                {
                    await stream.WriteAsync(new PullResponse
                    {
                        PollingResponse = lastPollingResponse,
                        OperationId = transactionId ?? string.Empty,
                    });
                }
                catch { }
                _logger.LogError(ex, ex.Message);
            }
        }

        public override async Task PullPackageTag(PullPackageTagRequest request, IServerStreamWriter<PullResponse> responseStream, ServerCallContext context)
        {
            await RunPullAsync(
                responseStream,
                async (onPollingResponse, onTransactionId) =>
                {
                    var pullResult = await _packageTagPuller.PullAsync(
                        _daemon,
                        request,
                        (response, _) => onPollingResponse(response),
                        context.CancellationToken);
                    onTransactionId(pullResult.TransactionId);
                });
        }

        public override async Task PullGitCommit(PullGitCommitRequest request, IServerStreamWriter<PullResponse> responseStream, ServerCallContext context)
        {
            await RunPullAsync(
                responseStream,
                async (onPollingResponse, onTransactionId) =>
                {
                    var pullResult = await _gitCommitPuller.PullAsync(
                        _daemon,
                        request,
                        (response, _) => onPollingResponse(response),
                        context.CancellationToken);
                    onTransactionId(pullResult.TransactionId);
                });
        }

        public override async Task Verify(VerifyRequest request, IServerStreamWriter<VerifyResponse> responseStream, ServerCallContext context)
        {
            string? transactionId = null;
            PollingResponse? lastPollingResponse = null;
            try
            {
                await using (var transaction = await _daemon.TransactionalDatabase.BeginTransactionAsync<VerifyPackagesTransactionRequest>(
                    new VerifyPackagesTransactionRequest
                    {
                        PackageFs = _daemon.PackageStorage.PackageFs,
                        Fix = request.Fix,
                        NoWait = request.NoWait,
                    },
                    async response =>
                    {
                        lastPollingResponse = response;
                        await responseStream.WriteAsync(new VerifyResponse
                        {
                            PollingResponse = lastPollingResponse,
                            OperationId = transactionId ?? string.Empty,
                        });
                    },
                    context.CancellationToken))
                {
                    transactionId = transaction.TransactionId;

                    if (!request.NoWait)
                    {
                        await transaction.WaitForCompletionAsync(context.CancellationToken);
                    }

                    if (!request.NoWait && lastPollingResponse == null)
                    {
                        _logger.LogError("Verification transaction finished without any polling response!");
                        lastPollingResponse = new PollingResponse
                        {
                            Type = PollingResponseType.Immediate,
                        };
                    }

                    await responseStream.WriteAsync(new VerifyResponse
                    {
                        PollingResponse = lastPollingResponse ?? new PollingResponse(),
                        OperationId = transactionId ?? string.Empty,
                    });
                }
            }
            catch (Exception ex)
            {
                if (lastPollingResponse == null)
                {
                    lastPollingResponse = new PollingResponse
                    {
                        Type = PollingResponseType.Immediate,
                    };
                }
                lastPollingResponse.Exception(ex);
                await responseStream.WriteAsync(new VerifyResponse
                {
                    PollingResponse = lastPollingResponse,
                    OperationId = transactionId ?? string.Empty,
                });
                _logger.LogError(ex, ex.Message);
            }
        }

        public override async Task<UnmountResponse> Unmount(UnmountRequest request, ServerCallContext context)
        {
            if (!_daemon.CurrentMounts.ContainsKey(request.MountId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No such mount exists."));
            }

            await using (var transaction = await _daemon.TransactionalDatabase.BeginTransactionAsync<RemoveMountTransactionRequest>(
                new RemoveMountTransactionRequest
                {
                    MountId = request.MountId,
                },
                _ => Task.CompletedTask,
                context.CancellationToken))
            {
                await transaction.WaitForCompletionAsync(context.CancellationToken);
                return new UnmountResponse();
            }
        }

        public override async Task<ListResponse> List(ListRequest request, ServerCallContext context)
        {
            await using (var transaction = await _daemon.TransactionalDatabase.BeginTransactionAsync<ListMountsTransactionRequest, ListResponse>(
                new ListMountsTransactionRequest
                {
                },
                (_, _) => Task.CompletedTask,
                context.CancellationToken))
            {
                return await transaction.WaitForCompletionAsync(context.CancellationToken);
            }
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            return Task<PingResponse>.FromResult(new PingResponse());
        }
    }
}
