namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ManifestServerComponent : IComponent, IAsyncDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _apiTask;
        private readonly ILogger<ManifestServerComponent> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IPathProvider _pathProvider;
        private readonly List<Func<Task>> _manifestNotifications;
        private ContainerdManifest _currentManifest;

        public ManifestServerComponent(
            ILogger<ManifestServerComponent> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IPathProvider pathProvider)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _pathProvider = pathProvider;
            _manifestNotifications = new List<Func<Task>>();
            _currentManifest = new ContainerdManifest
            {
                ContainerdInstallRootPath = Path.Combine(_pathProvider.RKMRoot, "containerd"),
                ContainerdStatePath = Path.Combine(_pathProvider.RKMRoot, "containerd-state"),
                ContainerdVersion = "1.6.18",
                UseRedpointContainerd = true,
                RuncVersion = "1.3.0",
                CniPluginsPath = Path.Combine(_pathProvider.RKMRoot, "cni-plugins"),
            };
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
        }

        private async Task RunAsync()
        {
            try
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:8375/");
                listener.Start();
                _logger.LogInformation($"Started rkm local manifest server on port 127.0.0.1:8375.");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cts!.Token,
                    _hostApplicationLifetime.ApplicationStopping);

                while (listener.IsListening && !cts.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().AsCancellable(cts.Token);

                    try
                    {
                        if (context.Request.Url?.AbsolutePath == "/containerd" &&
                            context.Request.IsWebSocketRequest)
                        {
                            var webSocket = await context.AcceptWebSocketAsync(null);

                            var handler = async () =>
                            {
                                _logger.LogInformation("Sending updated manifest for containerd...");
                                await webSocket.WebSocket.SendAsync(
                                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                                    WebSocketMessageType.Text,
                                    true,
                                    cts.Token);
                            };
                            _manifestNotifications.Add(handler);
                            try
                            {
                                _logger.LogInformation("Sending initial manifest for containerd...");
                                await webSocket.WebSocket.SendAsync(
                                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_currentManifest, ManifestJsonSerializerContext.Default.ContainerdManifest)),
                                    WebSocketMessageType.Text,
                                    true,
                                    cts.Token);

                                while (webSocket.WebSocket.State == WebSocketState.Open)
                                {
                                    var buffer = new byte[1024];
                                    await webSocket.WebSocket.ReceiveAsync(buffer, cts.Token);
                                }
                            }
                            finally
                            {
                                _manifestNotifications.Remove(handler);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
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
                        _logger.LogError(ex, $"Failed to respond to a containerd manifest request: {ex.Message}");
                    }
                    finally
                    {
                        context.Response.OutputStream.Close();
                    }
                }
            }
            catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested || (_cts?.IsCancellationRequested ?? false))
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"rkm local manifest server loop unexpectedly failed, which will cause rkm to shutdown as it will no longer be able to respond to new nodes: {ex.Message}");
            }
            finally
            {
                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                    !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested &&
                    !(_cts?.IsCancellationRequested ?? false))
                {
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                }
            }
        }

        private Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (_apiTask == null)
            {
                _logger.LogInformation("Starting local manifest server...");

                _cts = new CancellationTokenSource();
                _apiTask = Task.Run(RunAsync, CancellationToken.None);
            }

            return Task.CompletedTask;
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation("Stopping local manifest server...");

                _cts.Cancel();
                try
                {
                    await _apiTask;
                }
                catch { }
                _cts.Dispose();
                _apiTask = null;
                _cts = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation("Stopping local manifest server...");

                _cts.Cancel();
                try
                {
                    await _apiTask;
                }
                catch { }
                _cts.Dispose();
                _apiTask = null;
                _cts = null;
            }
        }
    }
}
