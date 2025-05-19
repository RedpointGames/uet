namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization;
    using System.Net;
    using System.Web;

    internal class DefaultNodeManifestClient : INodeManifestClient, IDisposable
    {
        private readonly IPathProvider _pathProvider;
        private readonly SemaphoreSlim _semaphore;

        public DefaultNodeManifestClient(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _semaphore = new SemaphoreSlim(1);
        }

        public void Dispose()
        {
            ((IDisposable)_semaphore).Dispose();
        }

        public async Task<NodeManifest> ObtainNodeManifestAsync(IPAddress controllerAddress, string nodeName, CancellationToken stoppingToken)
        {
            var manifestPath = Path.Combine(_pathProvider.RKMRoot, "node-manifest.yaml");
            var aotContext = new KubernetesYamlStaticContext();
            var deserializer = new StaticDeserializerBuilder(aotContext)
              .WithNamingConvention(CamelCaseNamingConvention.Instance)
              .Build();

            await _semaphore.WaitAsync(stoppingToken);
            try
            {
                if (File.Exists(manifestPath))
                {
                    return deserializer.Deserialize<NodeManifest>(await File.ReadAllTextAsync(manifestPath, stoppingToken));
                }

                using (var client = new HttpClient())
                {
                    var manifest = await client.GetStringAsync(new Uri($"http://{controllerAddress}:8374/manifest?nodeName=" + HttpUtility.UrlEncode(nodeName)), stoppingToken);
                    var manifestDeserialized = deserializer.Deserialize<NodeManifest>(manifest);
                    await File.WriteAllTextAsync(manifestPath, manifest, stoppingToken);
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
