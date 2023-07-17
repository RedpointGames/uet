namespace Redpoint.Git.Managed.Operation
{
    using Redpoint.Numerics;

    internal class CheckoutCommitGitOperation : GitOperation
    {
        public UInt160? PreviousCommit { get; set; }

        public UInt160 Commit { get; set; }

        public required DirectoryInfo GitDirectory { get; set; }

        public required DirectoryInfo TargetDirectory { get; set; }
    }
}