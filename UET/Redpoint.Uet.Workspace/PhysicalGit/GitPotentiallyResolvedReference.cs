namespace Redpoint.Uet.Workspace.PhysicalGit
{
    internal class GitPotentiallyResolvedReference
    {
        public required string TargetCommitOrUnresolvedReference { get; init; }
        public required bool TargetIsPotentialAnnotatedTag { get; init; }
    }
}
