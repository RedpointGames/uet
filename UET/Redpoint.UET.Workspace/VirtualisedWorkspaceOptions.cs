namespace Redpoint.UET.Workspace
{
    public record VirtualisedWorkspaceOptions
    {
        // @note: I intend to remove this option in the future, because the virtual workspace provider
        // should always be unmounting, but re-attaching the scratch write layer upon remount instead.
        public bool UnmountAfterUse { get; init; } = false;
    }
}
