namespace Docker.Registry.DotNet.Registry
{
    using System;

    using Docker.Registry.DotNet.Authentication;
    using Docker.Registry.DotNet.Endpoints;
    using Docker.Registry.DotNet.Endpoints.Implementations;

    internal sealed class RegistryClient : IRegistryClient
    {
        private readonly NetworkClient _client;
        public RegistryClient(
            RegistryClientConfiguration configuration,
            AuthenticationProvider authenticationProvider)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            if (authenticationProvider == null)
                throw new ArgumentNullException(nameof(authenticationProvider));

            _client = new NetworkClient(configuration, authenticationProvider);

            this.Manifest = new ManifestOperations(_client);
            this.Catalog = new CatalogOperations(_client);
            this.Blobs = new BlobOperations(_client);
            this.BlobUploads = new BlobUploadOperations(_client);
            this.System = new SystemOperations(_client);
            this.Tags = new TagOperations(_client);
        }

        public IBlobUploadOperations BlobUploads { get; }

        public IManifestOperations Manifest { get; }

        public ICatalogOperations Catalog { get; }

        public IBlobOperations Blobs { get; }

        public ITagOperations Tags { get; }

        public ISystemOperations System { get; }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}