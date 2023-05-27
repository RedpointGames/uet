namespace Redpoint.Git.Native
{
    public interface IGitRepoManagerFactory
    {
        IGitRepoManager CreateGitRepoManager(string gitRepoPath);
    }
}
