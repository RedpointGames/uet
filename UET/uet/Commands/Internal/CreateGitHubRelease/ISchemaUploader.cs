namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Threading.Tasks;

    internal interface ISchemaUploader
    {
        Task UpdateSchemaRepositoryAsync(
            string version,
            HttpClient client,
            CancellationToken cancellationToken);
    }
}
