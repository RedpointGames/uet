namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;

    public class CurrentFolderSnapshotUefsMount : CurrentUefsMount
    {
        public string SourcePath;

        public CurrentFolderSnapshotUefsMount(string sourcePath, string mountPath, IVfsDriver vfs)
        {
            SourcePath = sourcePath;
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
            };
        }
    }
}
