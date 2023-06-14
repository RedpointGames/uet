namespace Docker.Registry.DotNet.Endpoints.Implementations
{
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Docker.Registry.DotNet.Helpers;
    using Docker.Registry.DotNet.Models;
    using Docker.Registry.DotNet.Registry;

    internal class BlobOperations : IBlobOperations
    {
        private readonly NetworkClient _client;

        public BlobOperations(NetworkClient client)
        {
            this._client = client;
        }

        public async Task<GetBlobResponse> GetBlobAsync(
            string name,
            string digest,
            CancellationToken cancellationToken = default)
        {
            var url = $"v2/{name}/blobs/{digest}";

            var response = await this._client.MakeRequestForStreamedResponseAsync(
                               cancellationToken,
                               HttpMethod.Get,
                               url);

            return new GetBlobResponse(
                response.Headers.GetString("Docker-Content-Digest"),
                response.Body);
        }

        public Task DeleteBlobAsync(
            string name,
            string digest,
            CancellationToken cancellationToken = default)
        {
            var url = $"v2/{name}/blobs/{digest}";

            return this._client.MakeRequestAsync(cancellationToken, HttpMethod.Delete, url);
        }

        public async Task<bool> IsExistBlobAsync(string name, string digest, CancellationToken cancellationToken = default)
        {
            var path = $"v2/{name}/blobs/{digest}";

            var response = await this._client.MakeRequestNotErrorAsync(cancellationToken, HttpMethod.Head, path);
            
            if (response.StatusCode != HttpStatusCode.NotFound)
                this._client.HandleIfErrorResponse(response);

            return response.StatusCode == HttpStatusCode.OK;

        }
    }
}