﻿namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization.NamingConventions;
    using YamlDotNet.Serialization;
    using System.Net;
    using System.Web;
    using Microsoft.Extensions.Logging;
    using System.Net.Http.Headers;
    using System.Text.Json;

    internal class DefaultNodeManifestClient : INodeManifestClient, IDisposable
    {
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<DefaultNodeManifestClient> _logger;
        private readonly SemaphoreSlim _semaphore;

        public DefaultNodeManifestClient(
            IPathProvider pathProvider,
            ILogger<DefaultNodeManifestClient> logger)
        {
            _pathProvider = pathProvider;
            _logger = logger;
            _semaphore = new SemaphoreSlim(1);
        }

        public void Dispose()
        {
            ((IDisposable)_semaphore).Dispose();
        }

        public async Task<NodeManifest> ObtainNodeManifestAsync(IPAddress controllerAddress, string nodeName, CancellationToken stoppingToken)
        {
            var manifestPath = Path.Combine(_pathProvider.RKMRoot, "node-manifest.json");

        retry:
            await _semaphore.WaitAsync(stoppingToken);
            try
            {
                if (File.Exists(manifestPath))
                {
                    return JsonSerializer.Deserialize(
                        await File.ReadAllTextAsync(manifestPath, stoppingToken),
                        KubernetesJsonSerializerContext.Default.NodeManifest)!;
                }

                using (var client = new HttpClient())
                {
                    _logger.LogInformation($"Fetching manifest from: http://{controllerAddress}:8374/manifest");
                    var manifest = await client.GetStringAsync(new Uri($"http://{controllerAddress}:8374/manifest?nodeName=" + HttpUtility.UrlEncode(nodeName)), stoppingToken);
                    var manifestDeserialized = JsonSerializer.Deserialize(
                        manifest,
                        KubernetesJsonSerializerContext.Default.NodeManifest)!;
                    await File.WriteAllTextAsync(manifestPath, manifest, stoppingToken);
                    return manifestDeserialized;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to download manifest: {ex.Message}");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                goto retry;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
