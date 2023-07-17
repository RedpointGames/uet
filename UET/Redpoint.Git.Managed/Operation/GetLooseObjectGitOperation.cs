namespace Redpoint.Git.Managed.Operation
{
    using Redpoint.Concurrency;
    using Redpoint.Numerics;

    internal class GetLooseObjectGitOperation : GitOperation
    {
        public required DirectoryInfo GitDirectory { get; set; }

        public required UInt160 Sha { get; set; }

        public required FirstPastThePost<GitObjectInfo> Result { get; set; }
    }
}