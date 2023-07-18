using Redpoint.Numerics;
using Redpoint.Git.Managed;
using Redpoint.Git.Managed.Packfile;
using Microsoft.Extensions.DependencyInjection;
using Redpoint.Git.Managed.Operation;
using Microsoft.Extensions.Logging;
using Redpoint.Logging.SingleLine;
using System.Diagnostics;

var entries = new (UInt160 sha, GitObjectType type, ulong size)[]
{
    // from loose objects
    (UInt160.CreateFromString("0100f2c9dfbb00309df96b71b9ac4358c34f58bb"), GitObjectType.Blob, 416),
    (UInt160.CreateFromString("0123c2e0810c7430f870bb488421784639096702"), GitObjectType.Commit, 272),
    (UInt160.CreateFromString("01205f2a2b264b46834f19d759923d48675e7bdc"), GitObjectType.Tree, 216),
    // from the packfile
    (UInt160.CreateFromString("02b164123c4e7db3f4812b0339d619190bfc08e2"), GitObjectType.Blob, 112904),
    (UInt160.CreateFromString("09a9c8d1da459d0b54faaffeacc96c819da9b597"), GitObjectType.Commit, 272),
    (UInt160.CreateFromString("03e2d70896ac3429797887a20902de479107a41f"),
    GitObjectType.Tree, 1298),
};

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSingleLineConsoleFormatter();
    builder.AddSingleLineConsole();
});
var sp = services.BuildServiceProvider();

var logger = sp.GetRequiredService<ILogger<Program>>();

var engine = new GitExecutionEngine(
    sp.GetRequiredService<ILogger<GitExecutionEngine>>())
{
    OnInternalException = ex =>
    {
        logger.LogCritical(ex, ex.Message);
        Environment.Exit(1);
    }
};

var st = Stopwatch.StartNew();
const int count = 5000;
for (int i = 0; i < count; i++)
{
    foreach (var entry in entries)
    {
        var s = new SemaphoreSlim(0);
        engine.EnqueueOperation(new GetObjectGitOperation
        {
            GitDirectory = new DirectoryInfo("git"),
            Sha = entry.sha,
            OnResultAsync = result =>
            {
                try
                {
                    result?.Dispose();
                }
                finally
                {
                    s.Release();
                }
                return Task.CompletedTask;
            }
        });
        await s.WaitAsync();
    }
}
logger.LogInformation("avg fetch ms: " + (st.ElapsedMilliseconds / entries.Length / (double)count));