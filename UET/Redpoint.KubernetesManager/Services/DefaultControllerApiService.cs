namespace Redpoint.KubernetesManager.Services
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.Concurrency;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Hosting;
    using System.Text.Json;

    internal class DefaultControllerApiService : IControllerApiService
    {
        private readonly ILogger<DefaultControllerApiService> _logger;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateManager _certificateManager;
        private readonly IKubeConfigManager _kubeConfigManager;
        private Task? _apiTask = null;

        public DefaultControllerApiService(
            ILogger<DefaultControllerApiService> logger,
            ILocalEthernetInfo localEthernetInfo,
            IHostApplicationLifetime hostApplicationLifetime,
            IPathProvider pathProvider,
            ICertificateManager certificateManager,
            IKubeConfigManager kubeConfigManager)
        {
            _logger = logger;
            _localEthernetInfo = localEthernetInfo;
            _hostApplicationLifetime = hostApplicationLifetime;
            _pathProvider = pathProvider;
            _certificateManager = certificateManager;
            _kubeConfigManager = kubeConfigManager;
        }

        public void StartApiForNodes()
        {
            if (_apiTask == null)
            {
                _apiTask = Task.Run(RunApiAsync);
            }
        }

        private async Task RunApiAsync()
        {
            try
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add($"http://{_localEthernetInfo.IPAddress}:8374/");
                listener.Start();
                _logger.LogInformation($"Started rkm node API on port {_localEthernetInfo.IPAddress}:8374.");

                while (listener.IsListening && !_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().AsCancellable(_hostApplicationLifetime.ApplicationStopping);

                    try
                    {
                        if (context.Request.Url?.AbsolutePath == "/manifest")
                        {
                            var remoteAddress = context.Request.RemoteEndPoint.Address;
                            var nodeName = context.Request.QueryString.Get("nodeName");

                            var certificateAuthority = await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath("ca", "ca"));

                            var nodeCertificate = await _certificateManager.EnsureGeneratedForNodeAsync(nodeName!, remoteAddress);
                            var nodeKubeletConfig = await _kubeConfigManager.EnsureGeneratedForNodeAsync(certificateAuthority, nodeName!);

                            var nodeManifest = new NodeManifest
                            {
                                ServerRKMInstallationId = _pathProvider.RKMInstallationId,
                                NodeName = nodeName!,
                                CertificateAuthority = certificateAuthority,
                                NodeCertificate = nodeCertificate.CertificatePem,
                                NodeCertificateKey = nodeCertificate.PrivateKeyPem,
                                NodeKubeletConfig = nodeKubeletConfig,
                            };

                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.AddHeader("Content-Type", "text/yaml");
                            using (var writer = new StreamWriter(context.Response.OutputStream, leaveOpen: true))
                            {
                                await writer.WriteLineAsync(JsonSerializer.Serialize(
                                    nodeManifest,
                                    KubernetesJsonSerializerContext.Default.NodeManifest));
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        // Expected.
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        }
                        catch { }
                        _logger.LogError(ex, $"Failed to respond to a node metadata request: {ex.Message}");
                    }
                    finally
                    {
                        context.Response.OutputStream.Close();
                    }
                }
            }
            catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"rkm API server loop unexpectedly failed, which will cause rkm to shutdown as it will no longer be able to respond to new nodes: {ex.Message}");
            }
            finally
            {
                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                    !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested)
                {
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                }
            }
        }
    }
}
