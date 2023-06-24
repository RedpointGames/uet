namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;

    public class CurrentGitUefsMount : CurrentUefsMount
    {
        public string GitUrl;
        public string GitCommit;

        public CurrentGitUefsMount(string gitUrl, string gitCommit, string mountPath, IVfsDriver vfs)
        {
            GitUrl = gitUrl;
            GitCommit = gitCommit;
            MountPath = mountPath;
            _vfs = vfs;
        }

        private IVfsDriver? _vfs;

        public override void DisposeUnderlying()
        {
            if (_vfs != null)
            {
                _vfs.Dispose();
            }
        }

        public override Mount GetMountDescriptor(string id)
        {
            return new Mount
            {
                Id = id,
                MountPath = MountPath,
                StartupBehaviour = StartupBehaviour,
                WriteScratchPersistence = WriteScratchPersistence,
                GitCommit = GitCommit,
                GitUrl = GitUrl,
            };
        }
    }
}
