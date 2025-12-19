namespace UET.Commands.Internal.CreateGitHubRelease
{
    using Redpoint.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal interface IReleaseUploader
    {
        Task CreateVersionReleaseAsync(ICommandInvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client);

        Task UpdateLatestReleaseAsync(ICommandInvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client);
    }
}
