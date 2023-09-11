namespace Redpoint.Vfs.Layer.Git.Tests
{
    using LibGit2Sharp;
    using Redpoint.Git.Abstractions;
    using Redpoint.Git.Native;

    public class GitIndexTests
    {
        [SkippableFact]
        public async Task CanGenerateGitIndexForNativeRepository()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var cancellationToken = CancellationToken.None;
            var repository = new NativeGitRepository(new Repository(@"C:\Work\UE5\.git"));
            var index = new GitIndex();
            var commit = await repository.GetCommitByShaAsync("cdaec5b33ea5d332e51eee4e4866495c90442122", cancellationToken);
            var tree = await commit.GetRootTreeAsync(cancellationToken);
            var metrics = new GitTreeEnumerationMetrics(_ => { });
            await index.InitializeFromTreeAsync(tree, metrics, cancellationToken);
            using (var stream = new MemoryStream())
            {
                index.WriteTreeToStream(stream);
            }
        }

        [SkippableFact]
        public async Task CanRoundTripGeneratedGitIndexForNativeRepository()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var cancellationToken = CancellationToken.None;
            var repository = new NativeGitRepository(new Repository(@"C:\Work\UE5\.git"));
            var index = new GitIndex();
            var commit = await repository.GetCommitByShaAsync("cdaec5b33ea5d332e51eee4e4866495c90442122", cancellationToken);
            var tree = await commit.GetRootTreeAsync(cancellationToken);
            var metrics = new GitTreeEnumerationMetrics(_ => { });
            await index.InitializeFromTreeAsync(tree, metrics, cancellationToken);

            byte[] storage;
            using (var stream = new MemoryStream())
            {
                index.WriteTreeToStream(stream);
                stream.Flush();
                storage = new byte[stream.Position];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(storage);
            }

            using (var stream = new MemoryStream(storage))
            {
                var readIndex = new GitIndex();
                readIndex.ReadTreeFromStream(stream);

                Assert.Equal(readIndex._directories.Count, index._directories.Count);
                Assert.Equal(readIndex._files.Count, index._files.Count);
                Assert.Equal(readIndex._paths.Count, index._paths.Count);
            }
        }

        [SkippableFact]
        public async Task GeneratedGitIndexDoesNotContainAnyDotOrDotDotFiles()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\UE5\.git"), "Must have test Git repository checked out");

            var cancellationToken = CancellationToken.None;
            var repository = new NativeGitRepository(new Repository(@"C:\Work\UE5\.git"));
            var index = new GitIndex();
            var commit = await repository.GetCommitByShaAsync("cdaec5b33ea5d332e51eee4e4866495c90442122", cancellationToken);
            var tree = await commit.GetRootTreeAsync(cancellationToken);
            var metrics = new GitTreeEnumerationMetrics(_ => { });
            await index.InitializeFromTreeAsync(tree, metrics, cancellationToken);

            Assert.DoesNotContain(index._paths.Values, v => v.Name == ".");
            Assert.DoesNotContain(index._paths.Values, v => v.Name == "..");
            Assert.DoesNotContain(index._directories.SelectMany(v => v.Value), v => v.Name == ".");
            Assert.DoesNotContain(index._directories.SelectMany(v => v.Value), v => v.Name == "..");
        }
    }

}