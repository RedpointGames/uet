namespace Redpoint.Git.Managed.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Git.Managed.Operation;
    using Redpoint.Git.Managed.Packfile;
    using Redpoint.Numerics;
    using Redpoint.Tasks;
    using System.Diagnostics;

    public class ExecutionEngineTests
    {
        [Fact]
        public async Task CanRetrieveLooseAndPackedObjects()
        {
            var entries = new (UInt160 sha, GitObjectType type, ulong size)[]
            {
                // from loose objects
                (UInt160.CreateFromString("0100f2c9dfbb00309df96b71b9ac4358c34f58bb"), GitObjectType.Blob, 416),
                (UInt160.CreateFromString("0123c2e0810c7430f870bb488421784639096702"), GitObjectType.Commit, 272),
                (UInt160.CreateFromString("01205f2a2b264b46834f19d759923d48675e7bdc"),
                GitObjectType.Tree, 216),
                // from the packfile
                (UInt160.CreateFromString("02b164123c4e7db3f4812b0339d619190bfc08e2"), GitObjectType.Blob, 112904),
                (UInt160.CreateFromString("09a9c8d1da459d0b54faaffeacc96c819da9b597"), GitObjectType.Commit, 272),
                (UInt160.CreateFromString("03e2d70896ac3429797887a20902de479107a41f"),
                GitObjectType.Tree, 1298),
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTasks();
            var sp = services.BuildServiceProvider();

            using var engine = new GitExecutionEngine(
                sp.GetRequiredService<ILogger<GitExecutionEngine>>(),
                sp.GetRequiredService<ITaskScheduler>())
            {
                OnInternalException = ex =>
                {
                    Assert.Fail(ex.Message);
                }
            };

            foreach (var entry in entries)
            {
                var s = new Semaphore(0);
                engine.EnqueueOperation(new GetObjectGitOperation
                {
                    GitDirectory = new DirectoryInfo("git"),
                    Sha = entry.sha,
                    OnResultAsync = result =>
                    {
                        try
                        {
                            Assert.NotNull(result);
                            try
                            {
                                Assert.Equal(entry.type, result.Type);
                                Assert.Equal(entry.size, result.Size);
                            }
                            finally
                            {
                                result.Dispose();
                            }
                        }
                        finally
                        {
                            s.Release();
                        }
                        return Task.CompletedTask;
                    }
                });
                Assert.True(
                    await s.WaitAsync(5000, CancellationToken.None).ConfigureAwait(true),
                    "Expected Git operation to complete within 5 seconds");
            }
        }
    }
}