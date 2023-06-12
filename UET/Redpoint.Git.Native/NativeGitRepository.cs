namespace Redpoint.Git.Native
{
    using LibGit2Sharp;
    using Redpoint.Git.Abstractions;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides access to a local Git repository.
    /// </summary>
    public class NativeGitRepository : IGitRepository
    {
        private readonly Repository _repository;

        /// <summary>
        /// Constructs a <see cref="NativeGitRepository"/> which represents access to a local Git repository.
        /// </summary>
        /// <param name="repository">The libgit2 repository. This class takes ownership and disposes the <see cref="Repository"/> when it is itself disposed.</param>
        public NativeGitRepository(Repository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc />
        public Task<string?> ResolveRefToShaAsync(string @ref, CancellationToken cancellationToken)
        {
            return Task.FromResult(GitResolver.ResolveToCommitHash(_repository, @ref));
        }

        /// <inheritdoc />
        public Task<IGitCommit> GetCommitByShaAsync(string sha, CancellationToken cancellationToken)
        {
            return Task.FromResult<IGitCommit>(new NativeGitCommit(_repository, _repository.Lookup<Commit>(sha)));
        }

        /// <inheritdoc />
        public Task<long> MaterializeBlobToDiskByShaAsync(string sha, string destinationPath, Func<string, string>? contentAdjust, CancellationToken cancellationToken)
        {
            var blob = _repository.Lookup<Blob>(sha);
            using (var reader = blob.GetContentStream())
            {
                if (contentAdjust != null)
                {
                    using (var memory = new MemoryStream())
                    {
                        reader.CopyTo(memory);
                        var b = new byte[memory.Position];
                        memory.Seek(0, SeekOrigin.Begin);
                        memory.Read(b);

                        using (var writer = new StreamWriter(new FileStream(destinationPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None)))
                        {
                            writer.Write(contentAdjust(Encoding.UTF8.GetString(b)));
                        }
                    }
                }
                else
                {
                    using (var writer = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        reader.CopyTo(writer);
                    }
                }
            }
            return Task.FromResult(_repository.ObjectDatabase.RetrieveObjectMetadata(blob.Id).Size);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _repository.Dispose();
        }
    }
}
