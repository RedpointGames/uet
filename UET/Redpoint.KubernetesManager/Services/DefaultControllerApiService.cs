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
    using Redpoint.KubernetesManager.ControllerApi;

    internal class DefaultControllerApiService : IControllerApiService
    {
        private readonly ILogger<DefaultControllerApiService> _logger;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IEnumerable<IControllerEndpoint> _endpoints;
        private Task? _apiTask = null;

        public DefaultControllerApiService(
            ILogger<DefaultControllerApiService> logger,
            ILocalEthernetInfo localEthernetInfo,
            IHostApplicationLifetime hostApplicationLifetime,
            IEnumerable<IControllerEndpoint> endpoints)
        {
            _logger = logger;
            _localEthernetInfo = localEthernetInfo;
            _hostApplicationLifetime = hostApplicationLifetime;
            _endpoints = endpoints;
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
                        var handled = false;
                        foreach (var endpoint in _endpoints)
                        {
                            if (context.Request.Url?.AbsolutePath == endpoint.Path)
                            {
                                handled = true;
                                await endpoint.HandleAsync(context);
                                break;
                            }
                        }
                        if (!handled)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
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
