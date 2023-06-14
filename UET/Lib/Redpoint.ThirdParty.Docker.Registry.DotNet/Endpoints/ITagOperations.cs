namespace Docker.Registry.DotNet.Endpoints
{
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Models;

    using JetBrains.Annotations;

    public interface ITagOperations
    {
        [PublicAPI]
        Task<ListImageTagsResponse> ListImageTagsAsync(
            string name,
            ListImageTagsParameters parameters = null,
            CancellationToken cancellationToken = default);
    }
}