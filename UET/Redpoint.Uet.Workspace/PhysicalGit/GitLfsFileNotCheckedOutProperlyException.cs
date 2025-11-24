namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using System;

    public class GitLfsFileNotCheckedOutProperlyException : Exception
    {
        public GitLfsFileNotCheckedOutProperlyException(string fullName)
            : base($"Found Git LFS pointer in file: {fullName}")
        {
        }
    }
}
