namespace Docker.Registry.DotNet.Endpoints
{
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Models;

    using JetBrains.Annotations;

    public interface IBlobOperations
    {
        /// <summary>
        ///     Retrieve the blob from the registry identified by digest. Performs a monolithic download of the blob.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="digest"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [PublicAPI]
        Task<GetBlobResponse> GetBlobAsync(
            string name,
            string digest,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Delete the blob identified by name and digest.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="digest"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [PublicAPI]
        Task DeleteBlobAsync(
            string name,
            string digest,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Existing Layers
        /// </summary>
        /// <param name="name"></param>
        /// <param name="digest"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsExistBlobAsync(string name, string digest, CancellationToken cancellationToken = default);
    }
}