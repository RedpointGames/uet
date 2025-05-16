namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization;
    using System.Net;
    using System.Web;

    internal class DefaultNodeManifestClient : INodeManifestClient
    {
        private readonly IPathProvider _pathProvider;
        private readonly SemaphoreSlim _semaphore;

        public DefaultNodeManifestClient(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _semaphore = new SemaphoreSlim(1);
        }

        public async Task<NodeManifest> ObtainNodeManifestAsync(IPAddress controllerAddress, string nodeName, CancellationToken stoppingToken)
        {
            var manifestPath = Path.Combine(_pathProvider.RKMRoot, "node-manifest.yaml");
            var deserializer = new DeserializerBuilder()
              .WithNamingConvention(CamelCaseNamingConvention.Instance)
              .Build();

            await _semaphore.WaitAsync();
            try
            {
                if (File.Exists(manifestPath))
                {
                    return deserializer.Deserialize<NodeManifest>(await File.ReadAllTextAsync(manifestPath));
                }

                using (var client = new HttpClient())
                {
                    var manifest = await client.GetStringAsync($"http://{controllerAddress}:8374/manifest?nodeName=" + HttpUtility.UrlEncode(nodeName));
                    var manifestDeserialized = deserializer.Deserialize<NodeManifest>(manifest);
                    await File.WriteAllTextAsync(manifestPath, manifest);
                    return manifestDeserialized;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
