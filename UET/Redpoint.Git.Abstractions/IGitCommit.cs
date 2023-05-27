namespace Redpoint.Git.Abstractions
{
    public interface IGitCommit
    {
        DateTimeOffset CommittedAtUtc { get; }

        Task<IGitTree> GetRootTreeAsync(CancellationToken cancellationToken);
    }
}
