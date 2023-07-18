namespace Redpoint.Git.Managed.Operation
{
    using Redpoint.Numerics;

    internal class GetObjectGitOperation : GitOperation
    {
        public required DirectoryInfo GitDirectory { get; set; }

        public UInt160 Sha { get; set; }

        public required Func<GitObjectInfo?, Task> OnResultAsync { get; set; }
    }
}