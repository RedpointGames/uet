namespace Redpoint.Git.Native
{
    using System;
    using System.Threading.Tasks;

    public interface IGitRepoManager
    {
        bool HasCommit(string commit);

        Task Fetch(
            string url,
            string commit,
            string publicKeyFile,
            string privateKeyFile,
            Action<GitFetchProgressInfo> onProgress);

        void StopProcesses();
    }
}
