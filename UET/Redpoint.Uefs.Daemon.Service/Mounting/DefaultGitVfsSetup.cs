namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Driver;
    using Redpoint.Vfs.Layer.Folder;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.Layer.GitDependencies;
    using Redpoint.Vfs.Layer.Scratch;
    using System.Threading.Tasks;

    internal class DefaultGitVfsSetup : IGitVfsSetup
    {
        private readonly ILogger<DefaultGitVfsSetup> _logger;
        private readonly IGitDependenciesVfsLayerFactory _gitDependenciesVfsLayerFactory;
        private readonly IFolderVfsLayerFactory _folderVfsLayerFactory;
        private readonly IScratchVfsLayerFactory _scratchVfsLayerFactory;
        private readonly IWriteScratchPath _writeScratchPath;
        private readonly IVfsDriverFactory? _vfsDriverFactory;

        public DefaultGitVfsSetup(
            ILogger<DefaultGitVfsSetup> logger,
            IGitDependenciesVfsLayerFactory gitDependenciesVfsLayerFactory,
            IFolderVfsLayerFactory folderVfsLayerFactory,
            IScratchVfsLayerFactory scratchVfsLayerFactory,
            IWriteScratchPath writeScratchPath,
            IVfsDriverFactory? vfsDriverFactory = null)
        {
            _logger = logger;
            _gitDependenciesVfsLayerFactory = gitDependenciesVfsLayerFactory;
            _folderVfsLayerFactory = folderVfsLayerFactory;
            _scratchVfsLayerFactory = scratchVfsLayerFactory;
            _writeScratchPath = writeScratchPath;
            _vfsDriverFactory = vfsDriverFactory;
        }

        public async Task<(string, IVfsDriver)> MountAsync(
            IUefsDaemon daemon,
            MountRequest request,
            string gitRepoPath,
            string[] folderLayers,
            IGitVfsLayer gitLayer,
            string contextMessage)
        {
            // GitDependencies layer.
            var gitDeps = _gitDependenciesVfsLayerFactory.CreateLayer(
                Path.Combine(daemon.StoragePath, "git-deps"),
                gitLayer);
            await gitDeps.InitAsync(CancellationToken.None);

            // Add on console layers.
            IVfsLayer finalLayer = gitDeps;
            foreach (var layer in folderLayers)
            {
                var fullPath = Path.GetFullPath(layer);
                if (!Directory.Exists(fullPath))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"The additional layer at path '{fullPath}' does not exist."));
                }

                finalLayer = _folderVfsLayerFactory.CreateLayer(
                    fullPath,
                    finalLayer);
            }

            // Set up the scratch layer and VFS.
            var writeScratchPath = _writeScratchPath.ComputeWriteScratchPath(
                request,
                gitRepoPath);

            _logger.LogInformation($"{contextMessage} using WinFSP VFS, with write storage path at: {writeScratchPath}");
            finalLayer = _scratchVfsLayerFactory.CreateLayer(
                writeScratchPath,
                finalLayer);

            // Mount the VFS.
            var vfs = _vfsDriverFactory!.InitializeAndMount(
                finalLayer,
                request.MountPath,
                null)!;
            if (vfs == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unable to mount Git commit. Ensure WinFsp is installed on this system."));
            }

            // Store the mounted VFS.
            return (writeScratchPath, vfs);
        }
    }
}
