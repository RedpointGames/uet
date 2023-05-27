namespace Redpoint.Git.Abstractions
{
    public interface IGitRepository : IDisposable
    {
        Task<string?> ResolveRefToShaAsync(string @ref, CancellationToken cancellationToken);

        Task<IGitCommit> GetCommitByShaAsync(string sha, CancellationToken cancellationToken);

        Task<long> MaterializeBlobToDiskByShaAsync(string sha, string destinationPath, Func<string, string>? contentAdjust, CancellationToken cancellationToken);
    }
}
