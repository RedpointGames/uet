namespace Redpoint.Uet.Workspace.PhysicalGit
{
    internal class GitResolvedReference
    {
        public required string TargetCommit { get; init; }
        public required bool DidFetch { get; init; }
    }
}
