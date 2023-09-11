namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    internal sealed record class MountContext
    {
        public required string MountId { get; set; }
        public required int? TrackedPid { get; set; }
        public required bool IsBeingMountedOnStartup { get; set; }
    }
}
