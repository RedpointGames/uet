namespace Redpoint.UET.Workspace
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Core;
    using System.Threading.Tasks;

    internal class DefaultWorkspaceProvider : IWorkspaceProvider
    {
        private readonly IPathProvider _pathProvider;
        private readonly IBuildStabilityIdProvider _buildStabilityIdProvider;
        private readonly ILogger<DefaultWorkspaceProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly string _uesMountRoot;
        private readonly string _uefsPath;

        public DefaultWorkspaceProvider(
            IPathProvider pathProvider,
            IBuildStabilityIdProvider buildStabilityIdProvider,
            ILogger<DefaultWorkspaceProvider> logger,
            IProcessExecutor processExecutor)
        {
            _pathProvider = pathProvider;
            _buildStabilityIdProvider = buildStabilityIdProvider;
            _logger = logger;
            _processExecutor = processExecutor;
            _uesMountRoot = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
            _uefsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "UEFS",
                "uefs.exe");
            Directory.CreateDirectory(_uesMountRoot);
        }

        public Task<IWorkspace> GetLocalWorkspaceAsync()
        {
            _logger.LogInformation($"Creating local workspace: {_pathProvider.RepositoryRoot}");
            return Task.FromResult<IWorkspace>(new LocalWorkspace(_pathProvider.RepositoryRoot));
        }

        public Task<IWorkspace> GetFolderWorkspaceAsync(string folderPath)
        {
            _logger.LogInformation($"Creating workspace for existing path: {folderPath}");
            return Task.FromResult<IWorkspace>(new LocalWorkspace(folderPath));
        }

        public Task<IWorkspace> GetTempWorkspaceAsync(string name)
        {
            var tempPath = Path.Combine(_pathProvider.BuildScriptsTemp, name);
            Directory.CreateDirectory(tempPath);
            _logger.LogInformation($"Creating temporary workspace: {tempPath}");
            return Task.FromResult<IWorkspace>(new LocalWorkspace(tempPath));
        }

        public async Task<IWorkspace> GetGitWorkspaceAsync(
            string repository,
            string commit,
            string[] folders,
            string workspaceSuffix,
            CancellationToken cancellationToken)
        {
            var stabilityId = _buildStabilityIdProvider.GetBuildStabilityId(
                _pathProvider.RepositoryRoot,
                $"{repository}-{commit}-{string.Join("-", folders)}-{workspaceSuffix}",
                string.Empty);
            var targetPath = Path.Combine(
                _uesMountRoot,
                stabilityId);
            _logger.LogInformation($"Creating Git workspace using UEFS ({repository}, {commit}): {targetPath}");
            var scratchPath = Path.Combine(_uesMountRoot, $"Scratch-{stabilityId}");
            Directory.CreateDirectory(scratchPath);
            var argumentList = new List<string>
            {
                "mount",
                "--git-url",
                repository,
                "--git-commit",
                commit,
                "--dir",
                targetPath,
                "--scratch-path",
                scratchPath,
            };
            foreach (var folder in folders)
            {
                argumentList.Add("--with-layer");
                argumentList.Add(folder);
            }
            try
            {
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = _uefsPath,
                        Arguments = argumentList
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Unable to mount folder with UEFS; got exit code {exitCode}.");
                }
                return new UEFSWorkspace(_processExecutor, _uefsPath, targetPath);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = _uefsPath,
                            Arguments = new[] { "unmount", "--dir", targetPath },
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None);
                }
            }
        }

        public async Task<IWorkspace> GetPackageWorkspaceAsync(
            string tag,
            string workspaceSuffix,
            CancellationToken cancellationToken)
        {
            var stabilityId = _buildStabilityIdProvider.GetBuildStabilityId(
                _pathProvider.RepositoryRoot,
                $"{tag}-{workspaceSuffix}",
                string.Empty);
            var targetPath = Path.Combine(
                _uesMountRoot,
                stabilityId);
            _logger.LogInformation($"Creating package workspace using UEFS ({tag}): {targetPath}");
            try
            {
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = _uefsPath,
                        Arguments = new[]
                        {
                            "mount",
                            "--tag",
                            tag,
                            "--dir",
                            targetPath,
                        },
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Unable to mount folder with UEFS; got exit code {exitCode}.");
                }
                return new UEFSWorkspace(_processExecutor, _uefsPath, targetPath);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = _uefsPath,
                            Arguments = new[] { "unmount", "--dir", targetPath },
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None);
                }
            }
        }
    }
}
