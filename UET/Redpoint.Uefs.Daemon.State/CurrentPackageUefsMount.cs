namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;

    public class CurrentPackageUefsMount : CurrentUefsMount
    {
        public string PackagePath;
        public string? TagHint;

        public CurrentPackageUefsMount(string packagePath, string mountPath, string? tagHint, IAsyncDisposable packageMounter)
        {
            PackagePath = packagePath;
            MountPath = mountPath;
            TagHint = tagHint;
            _packageMounter = packageMounter;
        }

        private IAsyncDisposable? _packageMounter;

        public override async ValueTask DisposeUnderlyingAsync()
        {
            if (_packageMounter != null)
            {
                await _packageMounter.DisposeAsync().ConfigureAwait(false);
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
