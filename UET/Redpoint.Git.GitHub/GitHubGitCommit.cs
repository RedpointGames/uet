namespace Redpoint.Git.GitHub
{
    using Octokit;
    using Redpoint.Git.Abstractions;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class GitHubGitCommit : IGitCommit
    {
        private GitHubClient _client;
        private string _owner;
        private string _repo;
        private Commit _commit;

        public GitHubGitCommit(GitHubClient client, string owner, string repo, Commit commit)
        {
            _client = client;
            _owner = owner;
            _repo = repo;
            _commit = commit;
        }

        public DateTimeOffset CommittedAtUtc => _commit.Committer.Date;

        public Task<IGitTree> GetRootTreeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IGitTree>(new GitHubGitTree(_client, _owner, _repo, _commit.Tree.Sha, CommittedAtUtc));
        }
    }
}
