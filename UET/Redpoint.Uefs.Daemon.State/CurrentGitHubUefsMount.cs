namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;

    public class CurrentGitHubUefsMount : CurrentUefsMount
    {
        public string GitHubOwner;
        public string GitHubRepo;
        public string GitHubCommit;

        public CurrentGitHubUefsMount(string owner, string repo, string commit, string mountPath, IVfsDriver vfs)
        {
            GitHubOwner = owner;
            GitHubRepo = repo;
            GitHubCommit = commit;
            MountPath = mountPath;
            _vfs = vfs;
        }

        private IVfsDriver? _vfs;

        public override ValueTask DisposeUnderlyingAsync()
        {
            if (_vfs != null)
            {
                _vfs.Dispose();
            }
            return ValueTask.CompletedTask;
        }

        public override Mount GetMountDescriptor(string id)
        {
            return new Mount
            {
                Id = id,
                MountPath = MountPath,
                StartupBehaviour = StartupBehaviour,
                WriteScratchPersistence = WriteScratchPersistence,
                GitCommit = GitHubCommit,
                GitUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}",
            };
        }
    }
}
