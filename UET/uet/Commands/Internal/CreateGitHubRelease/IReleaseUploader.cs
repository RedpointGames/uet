namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal interface IReleaseUploader
    {
        Task CreateVersionReleaseAsync(InvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client);

        Task UpdateLatestReleaseAsync(InvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client);
    }
}
