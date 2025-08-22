namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using System;

    public class GitRepositoryCorruptException : Exception
    {
        public GitRepositoryCorruptException()
            : base("The Git repository is corrupt and must be re-initialized.")
        {
        }
    }
}
