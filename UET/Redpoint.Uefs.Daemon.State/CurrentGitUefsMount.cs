namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;
    using System.Diagnostics.CodeAnalysis;

    public class CurrentGitUefsMount : CurrentUefsMount
    {
        public string GitUrl;
        public string GitCommit;

        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "We intentionally want to use a string value for the Git URL.")]
        public CurrentGitUefsMount(string gitUrl, string gitCommit, string mountPath, IVfsDriver vfs)
        {
            GitUrl = gitUrl;
            GitCommit = gitCommit;
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
                GitCommit = GitCommit,
                GitUrl = GitUrl,
            };
        }
    }
}
