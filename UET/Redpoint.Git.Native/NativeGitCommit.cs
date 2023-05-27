namespace Redpoint.Git.Native
{
    using LibGit2Sharp;
    using Redpoint.Git.Abstractions;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class NativeGitCommit : IGitCommit
    {
        private readonly Repository _repository;
        private readonly Commit _commit;

        public NativeGitCommit(
            Repository repository,
            Commit commit)
        {
            _repository = repository;
            _commit = commit;
        }

        public DateTimeOffset CommittedAtUtc => _commit.Committer.When;

        public Task<IGitTree> GetRootTreeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IGitTree>(new NativeGitTree(_repository, _commit.Tree, _commit.Committer.When));
        }
    }
}
