namespace Redpoint.Git.Managed.Operation
{
    using Redpoint.Concurrency;
    using Redpoint.Numerics;

    internal class GetObjectFromPackfileGitOperation : GitOperation
    {
        public required FileInfo Packfile { get; set; }

        public required UInt160 Sha { get; set; }

        public required FirstPastThePost<GitObjectInfo> Result { get; set; }
    }
}