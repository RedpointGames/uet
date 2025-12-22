namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Kestrel;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    internal abstract class AbstractHttpListenerComponent : IComponent, IAsyncDisposable, IKestrelRequestHandler
    {
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IKestrelFactory _kestrelFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICertificateManager? _certificateManager;

        private CancellationTokenSource? _cts;
        private Task? _apiTask;

        public AbstractHttpListenerComponent(
            ILogger logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hostApplicationLifetime = _serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            _kestrelFactory = _serviceProvider.GetRequiredService<IKestrelFactory>();
            _certificateManager = _serviceProvider.GetService<ICertificateManager>();
        }

        protected abstract string ServerDescription { get; }

        protected abstract IPAddress ListeningAddress { get; }

        protected abstract int ListeningPort { get; }

        protected abstract int? SecureListeningPort { get; }

        protected abstract bool IsControllerOnly { get; }

        protected abstract Task HandleIncomingRequestAsync(HttpContext context, CancellationToken cancellationToken);

        public void RegisterSignals(IRegistrationContext context)
        {
            if (!IsControllerOnly || context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
                context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
            }
        }

        private async Task RunAsync(IContext rkmContext)
        {
            try
            {
                X509Certificate2? serverCertificate = null;
                if (SecureListeningPort.HasValue && _certificateManager != null)
                {
                    var certificate = await _certificateManager.GenerateCertificateForRequirementAsync(new CertificateRequirement
                    {
                        CommonName = "rkm-api",
                    });
                    serverCertificate = X509Certificate2.CreateFromPem(
                        certificate.CertificatePem,
                        certificate.PrivateKeyPem);
                }

                var kestrelServerOptions = new KestrelServerOptions();
                kestrelServerOptions.ApplicationServices = _serviceProvider;

                if (ListeningAddress == IPAddress.Loopback)
                {
                    kestrelServerOptions.ListenLocalhost(ListeningPort);
                }
                else
                {
                    kestrelServerOptions.ListenAnyIP(ListeningPort);
                    if (SecureListeningPort.HasValue && _certificateManager != null)
                    {
                        kestrelServerOptions.ListenAnyIP(
                            SecureListeningPort.Value,
                            options =>
                            {
                                options.UseHttps(
                                    httpsOptions =>
                                    {
                                        httpsOptions.ServerCertificate = serverCertificate!;
                                    });
                            });
                    }
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cts!.Token,
                    _hostApplicationLifetime.ApplicationStopping);

                using var kestrel = await _kestrelFactory.CreateAndStartServerAsync(
                    kestrelServerOptions,
                    this,
                    cts.Token);
                _logger.LogInformation($"Started rkm {ServerDescription} on port {ListeningAddress}:{ListeningPort}.");

                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(60000, cts.Token);
                }
            }
            catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested || (_cts?.IsCancellationRequested ?? false))
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"rkm {ServerDescription} loop unexpectedly failed, which will cause rkm to shutdown as it will no longer be able to respond: {ex.Message}");
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

        protected abstract Task OnStartingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken);

        protected virtual Task OnCleanupAsync()
        {
            return Task.CompletedTask;
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await OnStartingAsync(context, data, cancellationToken).ConfigureAwait(false);

            if (_apiTask == null)
            {
                _logger.LogInformation($"Starting rkm {ServerDescription}...");

                _cts = new CancellationTokenSource();
                _apiTask = Task.Run(async () => await RunAsync(context), CancellationToken.None);
            }
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await OnCleanupAsync();

            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation($"Stopping rkm {ServerDescription}...");

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
            await OnCleanupAsync();

            if (_apiTask != null && _cts != null)
            {
                _logger.LogInformation($"Stopping rkm {ServerDescription}...");

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

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            try
            {
                await HandleIncomingRequestAsync(httpContext, httpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested ?? true)
            {
                // Expected.
            }
            catch (Exception ex)
            {
                try
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                catch { }

                _logger.LogError(ex, $"Encountered an exception while handling HTTP request: {ex.Message}");
            }
        }
    }
}
