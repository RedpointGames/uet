namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;

    public class CurrentPackageUefsMount : CurrentUefsMount
    {
        public string PackagePath;
        public string? TagHint;

        public CurrentPackageUefsMount(string packagePath, string mountPath, string? tagHint, IDisposable packageMounter)
        {
            PackagePath = packagePath;
            MountPath = mountPath;
            TagHint = tagHint;
            _packageMounter = packageMounter;
        }

        private IDisposable? _packageMounter;

        public override void DisposeUnderlying()
        {
            if (_packageMounter != null)
            {
                _packageMounter.Dispose();
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
                PackagePath = PackagePath,
                TagHint = TagHint ?? string.Empty,
            };
        }
    }
}
