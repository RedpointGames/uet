namespace Redpoint.Vfs.Client
{
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal class MacVfsClient : IVfsClient
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;

        public MacVfsClient(
            IProcessExecutor processExecutor,
            IPathResolver pathResolver)
        {
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
        }

        public async Task MountAsync(string vfsHostName, Uri sourceUri, string mountPath)
        {
            var mount = await _pathResolver.ResolveBinaryPath("mount");

            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = mount,
                    Arguments = ["-t", vfsHostName, sourceUri.ToString(), mountPath]
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);

            if (exitCode != 0)
            {
                throw new MountFailureException();
            }
        }

        public async Task UnmountAsync(string vfsHostName, string mountPath)
        {
            var umount = await _pathResolver.ResolveBinaryPath("umount");

            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = umount,
                    Arguments = [mountPath]
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);

            if (exitCode != 0)
            {
                throw new MountFailureException();
            }
        }
    }
}
