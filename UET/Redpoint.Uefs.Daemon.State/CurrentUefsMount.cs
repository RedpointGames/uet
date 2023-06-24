namespace Redpoint.Uefs.Daemon.State
{
    using Redpoint.Uefs.Protocol;

    public abstract class CurrentUefsMount
    {
        public string? MountPath;

        public WriteScratchPersistence WriteScratchPersistence;

        public StartupBehaviour StartupBehaviour;

        public int? TrackPid { get; set; }

        public abstract void DisposeUnderlying();

        public abstract Mount GetMountDescriptor(string id);
    }
}
