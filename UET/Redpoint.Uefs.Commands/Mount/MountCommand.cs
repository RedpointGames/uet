namespace Redpoint.Uefs.Commands.Mount
{
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Protocol;
    using Redpoint.ProcessTree;
    using static Redpoint.Uefs.Protocol.Uefs;
    using System.Diagnostics;
    using Redpoint.CredentialDiscovery;

    public static class MountCommand
    {
        internal class Options
        {
            public Option<FileInfo> PackagePath = new Option<FileInfo>("--pkg", description: "The path to the package to mount.");
            public Option<string> PackageTag = new Option<string>("--tag", description: "The tag of the package to mount. The package will be pulled from the registry if the local copy is out of date.");
            public Option<string> GitUrl = new Option<string>("--git-url", description: "The Git repository URL to mount.");
            public Option<string> GitCommit = new Option<string>("--git-commit", description: "The Git commit to mount.");
            public Option<string[]> WithLayer = new Option<string[]>("--with-layer", description: "Additional folders to layer on top of the projected Git commit (such as console folders).");
            public Option<DirectoryInfo> MountPath = new Option<DirectoryInfo>("--dir", description: "The path to project the mounted package into.") { IsRequired = true };
            public Option<bool> TrackParent = new Option<bool>("--track-parent", description: "If passed, the UEFS daemon will detect when the process invoking UEFS exits, and automatically unmount the package when it does so.");
            public Option<string> Persist = new Option<string>("--persist", description: "Either 'none' (the default), 'ro' or 'rw'. If set to 'ro', this will be mounted at startup with all previous writes discarded. If set to 'rw', this will be mounted at startup with writes carried across reboots.");
            public Option<string> ScratchPath = new Option<string>("--scratch-path", description: "The path to store copy-on-write data. This will always be a folder, with the write data underneath.");
            public Option<DirectoryInfo> FolderSnapshot = new Option<DirectoryInfo>("--folder-snapshot", description: "The path to serve a snapshot of. All writes to the mounted folder will be redirected to the scratch path.");
        }

        public static Command CreateMountCommand()
        {
            var options = new Options();
            var command = new Command("mount", "Mounts a UEFS package on the local system.");
            command.AddAllOptions(options);
            command.AddCommonHandler<MountCommandInstance>(options);
            return command;
        }

        private class MountCommandInstance : ICommandInstance
        {
            private readonly IMonitorFactory _monitorFactory;
            private readonly IProcessTree _processTree;
            private readonly ICredentialDiscovery _credentialDiscovery;
            private readonly UefsClient _uefsClient;
            private readonly IRetryableGrpc _retryableGrpc;
            private readonly Options _options;

            public MountCommandInstance(
                IMonitorFactory monitorFactory,
                IProcessTree processTree,
                ICredentialDiscovery credentialDiscovery,
                UefsClient uefsClient,
                IRetryableGrpc retryableGrpc,
                Options options)
            {
                _monitorFactory = monitorFactory;
                _processTree = processTree;
                _credentialDiscovery = credentialDiscovery;
                _uefsClient = uefsClient;
                _retryableGrpc = retryableGrpc;
                _options = options;
            }

            private enum MountType
            {
                Unknown,
                PackageFile,
                PackageTag,
                GitCommit,
                FolderSnapshot,
            }

            private async Task<string> MountAsync<TRequest>(
                Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<MountResponse>> call,
                TRequest request,
                CancellationToken cancellationToken)
            {
                var operation = new ObservableMountOperation<TRequest>(
                    _retryableGrpc,
                    _monitorFactory,
                    call,
                    request,
                    TimeSpan.FromSeconds(60),
                    cancellationToken);
                return await operation.RunAndWaitForMountIdAsync();
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var packagePath = context.ParseResult.GetValueForOption(_options.PackagePath);
                var packageTag = context.ParseResult.GetValueForOption(_options.PackageTag);
                var gitUrl = context.ParseResult.GetValueForOption(_options.GitUrl);
                var gitCommit = context.ParseResult.GetValueForOption(_options.GitCommit);
                var withLayer = context.ParseResult.GetValueForOption(_options.WithLayer);
                var mountPath = context.ParseResult.GetValueForOption(_options.MountPath)!;
                var trackParent = context.ParseResult.GetValueForOption(_options.TrackParent);
                var persist = context.ParseResult.GetValueForOption(_options.Persist);
                var scratchPath = context.ParseResult.GetValueForOption(_options.ScratchPath);
                var folderSnapshot = context.ParseResult.GetValueForOption(_options.FolderSnapshot);

                WriteScratchPersistence writeScratchPersistence = WriteScratchPersistence.DiscardOnUnmount;
                StartupBehaviour startupBehaviour = StartupBehaviour.None;
                if (persist != null)
                {
                    switch (persist)
                    {
                        case "":
                        case "none":
                            writeScratchPersistence = WriteScratchPersistence.DiscardOnUnmount;
                            startupBehaviour = StartupBehaviour.None;
                            break;
                        case "ro":
                            writeScratchPersistence = WriteScratchPersistence.DiscardOnUnmount;
                            startupBehaviour = StartupBehaviour.MountOnStartup;
                            break;
                        case "rw":
                            writeScratchPersistence = WriteScratchPersistence.Keep;
                            startupBehaviour = StartupBehaviour.MountOnStartup;
                            break;
                        default:
                            Console.Error.WriteLine($"error: unknown persist mode '{persist}");
                            return 1;
                    }
                }

                if (trackParent && startupBehaviour == StartupBehaviour.MountOnStartup)
                {
                    Console.Error.WriteLine("error: you can not pass --persist with --track-parent");
                    return 1;
                }

                var mountType = MountType.Unknown;
                if (packagePath != null && packagePath.Exists &&
                    string.IsNullOrWhiteSpace(packageTag) &&
                    string.IsNullOrWhiteSpace(gitUrl) &&
                    string.IsNullOrWhiteSpace(gitCommit) &&
                    folderSnapshot == null)
                {
                    mountType = MountType.PackageFile;
                }
                else if (packagePath == null &&
                    !string.IsNullOrWhiteSpace(packageTag) &&
                    string.IsNullOrWhiteSpace(gitUrl) &&
                    string.IsNullOrWhiteSpace(gitCommit) &&
                    folderSnapshot == null)
                {
                    mountType = MountType.PackageTag;
                }
                else if (packagePath == null &&
                    string.IsNullOrWhiteSpace(packageTag) &&
                    !string.IsNullOrWhiteSpace(gitUrl) &&
                    !string.IsNullOrWhiteSpace(gitCommit) &&
                    folderSnapshot == null)
                {
                    mountType = MountType.GitCommit;
                }
                else if (packagePath == null &&
                    string.IsNullOrWhiteSpace(packageTag) &&
                    string.IsNullOrWhiteSpace(gitUrl) &&
                    string.IsNullOrWhiteSpace(gitCommit) &&
                    folderSnapshot != null)
                {
                    mountType = MountType.FolderSnapshot;
                }

                if (mountType == MountType.Unknown)
                {
                    Console.Error.WriteLine("error: expected --tag or --pkg or --folder-snapshot or both --git-url and --git-commit");
                    return 1;
                }

                int trackPid = 0;
                if (trackParent)
                {
                    Process? parentProcess = _processTree.GetParentProcess();
                    while (parentProcess != null && (parentProcess.ProcessName.Contains("uefs") || parentProcess.ProcessName.Contains("dotnet")))
                    {
                        parentProcess = _processTree.GetParentProcess(parentProcess.Id);
                    }
                    trackPid = parentProcess?.Id ?? 0;
                }

                var mountRequest = new MountRequest
                {
                    MountPath = mountPath.FullName,
                    TrackPid = trackPid,
                    WriteScratchPersistence = writeScratchPersistence,
                    StartupBehaviour = startupBehaviour,
                };

                try
                {
                    string mountId;
                    switch (mountType)
                    {
                        case MountType.PackageFile:
                            mountId = await MountAsync(
                                _uefsClient.MountPackageFile,
                                new MountPackageFileRequest
                                {
                                    MountRequest = mountRequest,
                                    Path = packagePath!.FullName,
                                },
                                context.GetCancellationToken());
                            break;
                        case MountType.PackageTag:
                            mountId = await MountAsync(
                                _uefsClient.MountPackageTag,
                                new MountPackageTagRequest
                                {
                                    MountRequest = mountRequest,
                                    Tag = packageTag,
                                    Credential = _credentialDiscovery.GetRegistryCredential(packageTag!),
                                },
                                context.GetCancellationToken());
                            break;
                        case MountType.GitCommit:
                            var mountGitCommitRequest = new MountGitCommitRequest
                            {
                                MountRequest = mountRequest,
                                Url = gitUrl,
                                Commit = gitCommit,
                                Credential = _credentialDiscovery.GetGitCredential(gitUrl!),
                            };
                            if (withLayer != null)
                            {
                                mountGitCommitRequest.FolderLayers.AddRange(withLayer);
                            }
                            mountId = await MountAsync(
                                _uefsClient.MountGitCommit,
                                mountGitCommitRequest,
                                context.GetCancellationToken());
                            break;
                        case MountType.FolderSnapshot:
                            mountId = await MountAsync(
                                _uefsClient.MountFolderSnapshot,
                                new MountFolderSnapshotRequest
                                {
                                    MountRequest = mountRequest,
                                    SourcePath = folderSnapshot!.FullName,
                                },
                                context.GetCancellationToken());
                            break;
                        default:
                            throw new InvalidOperationException("unknown mount type");
                    }
                    Console.WriteLine($"successfully mounted, mount ID is: {mountId}");
                    return 0;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"error: failed to mount: {ex.Message}");
                    return 1;
                }
            }
        }
    }
}
