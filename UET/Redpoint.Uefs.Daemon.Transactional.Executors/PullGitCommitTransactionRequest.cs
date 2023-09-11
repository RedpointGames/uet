namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Diagnostics.CodeAnalysis;

    public record class PullGitCommitTransactionRequest : ITransactionRequest, IBackgroundableTransactionRequest
    {
        public required IGitRepoManager GitRepoManager { get; set; }
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Git URLs may not be compatible with the Uri object.")]
        public required string GitUrl { get; set; }
        public required string GitCommit { get; set; }
        public required GitCredential Credential { get; set; }
        public required bool NoWait { get; set; }
    }
}
