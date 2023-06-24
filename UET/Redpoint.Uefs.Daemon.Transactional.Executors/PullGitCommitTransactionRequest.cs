namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;

    public record class PullGitCommitTransactionRequest : ITransactionRequest, IBackgroundableTransactionRequest
    {
        public required IGitRepoManager GitRepoManager { get; set; }
        public required string GitUrl { get; set; }
        public required string GitCommit { get; set; }
        public required GitCredential Credential { get; set; }
        public required bool NoWait { get; set; }
    }
}
