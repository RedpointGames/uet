#if GIT_NATIVE_CODE_ENABLED

namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;
    using Redpoint.Vfs.Layer.Git;
    using System.Threading.Tasks;

    internal interface IGitVfsSetup
    {
        Task<(string writeScratchPath, IVfsDriver vfs)> MountAsync(
            IUefsDaemon daemon,
            MountRequest request,
            string gitRepoPath,
            string[] folderLayers,
            IGitVfsLayer gitLayer,
            string contextMessage);
    }
}

#endif