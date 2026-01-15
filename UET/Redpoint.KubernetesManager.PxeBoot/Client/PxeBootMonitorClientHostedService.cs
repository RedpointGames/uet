namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.ServiceControl;
    using Redpoint.Tpm;
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Json;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PxeBootMonitorClientHostedService : IHostedService, IAsyncDisposable
    {
        private readonly ILogger<PxeBootMonitorClientHostedService> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IDurableOperation _durableOperation;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
        private readonly ICommandInvocationContext _commandInvocationContext;
        private readonly IReboot _reboot;
        private readonly IServiceControl _serviceControl;
        private readonly PxeBootMonitorClientOptions _options;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _task;

        public PxeBootMonitorClientHostedService(
            ILogger<PxeBootMonitorClientHostedService> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IDurableOperation durableOperation,
            ITpmSecuredHttp tpmSecuredHttp,
            ICommandInvocationContext commandInvocationContext,
            IReboot reboot,
            IServiceControl serviceControl,
            PxeBootMonitorClientOptions options)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _durableOperation = durableOperation;
            _tpmSecuredHttp = tpmSecuredHttp;
            _commandInvocationContext = commandInvocationContext;
            _reboot = reboot;
            _serviceControl = serviceControl;
            _options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StopAsync(cancellationToken);

            _cancellationTokenSource = new CancellationTokenSource();
            _task = Task.Run(
                async () => await RunAsync(_cancellationTokenSource.Token),
                cancellationToken);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                var clientFactory = await _durableOperation.DurableOperationAsync(
                    async cancellationToken =>
                    {
                        return await _tpmSecuredHttp.CreateHttpClientFactoryAsync(
                            new Uri($"http://{_commandInvocationContext.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}:8790/api/node-provisioning/negotiate-certificate"),
                            cancellationToken);
                    },
                    cancellationToken);
                var client = clientFactory.Create();
                client.Timeout = TimeSpan.FromSeconds(5);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            _logger.LogInformation("Checking what services we should ensure are always running on this machine...");
                            var servicesResponse = await _durableOperation.DurableOperationAsync(
                                async cancellationToken =>
                                {
                                    return await client.GetAsync(
                                        new Uri($"https://{_commandInvocationContext.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}:8791/api/node-provisioning/query-services"),
                                        cancellationToken);
                                },
                                cancellationToken);
                            if (servicesResponse.IsSuccessStatusCode)
                            {
                                var services = await servicesResponse.Content.ReadFromJsonAsync(
                                    KubernetesRkmJsonSerializerContext.Default.RkmNodeGroupSpecServices,
                                    cancellationToken);
                                if (services != null)
                                {
                                    foreach (var keepAlive in services?.KeepAlive ?? [])
                                    {
                                        if (string.IsNullOrWhiteSpace(keepAlive))
                                        {
                                            continue;
                                        }

                                        if (await _serviceControl.IsServiceInstalled(keepAlive))
                                        {
                                            if (!(await _serviceControl.IsServiceRunning(keepAlive, cancellationToken)))
                                            {
                                                _logger.LogWarning($"Service marked for keep alive is not currently running; it will be started: {keepAlive}");
                                                try
                                                {
                                                    await _serviceControl.StartService(keepAlive, cancellationToken);
                                                    _logger.LogInformation("Request to start the service succeeded.");
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, $"Failed to request the service to start: {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.Message);
                        }

                        try
                        {
                            var rebootResponse = await _durableOperation.DurableOperationAsync(
                                async cancellationToken =>
                                {
                                    return await client.GetAsync(
                                        new Uri($"https://{_commandInvocationContext.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}:8791/api/node-provisioning/query-reboot-required"),
                                        cancellationToken);
                                },
                                cancellationToken);
                            if (rebootResponse.StatusCode == HttpStatusCode.Conflict ||
                                rebootResponse.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                _logger.LogInformation("Machine requires reprovisioning. Rebooting.");
                                await _reboot.RebootMachine(cancellationToken);
                                return;
                            }

                            if (rebootResponse.StatusCode == HttpStatusCode.NoContent)
                            {
                                _logger.LogInformation("Machine is up-to-date and does not require reprovisioning.");
                            }
                            else if (rebootResponse.StatusCode == HttpStatusCode.UnprocessableEntity)
                            {
                                _logger.LogWarning("Machine is not configured correctly for reprovisioning, but requires it. Not doing anything since reboot will not be successful.");
                            }
                            else
                            {
                                _logger.LogError($"Unknown status code from query reboot endpoint: {rebootResponse.StatusCode}");
                            }

                            int waitMilliseconds = 60000;
                            if (rebootResponse.Headers.TryGetValues("Wait-Milliseconds", out var waitMillisecondsValues) &&
                                waitMillisecondsValues.Count() == 1 &&
                                int.TryParse(waitMillisecondsValues.First(), out var waitMillisecondsOverride))
                            {
                                waitMilliseconds = waitMillisecondsOverride;
                            }

                            _logger.LogInformation($"Waiting {waitMilliseconds} milliseconds before checking again...");
                            await Task.Delay(waitMilliseconds, cancellationToken);
                        }
                        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException authEx)
                        {
                            // This will happen if the provisioner API restarts and generates a new certificate authority.
                            client.Dispose();
                            clientFactory = await _durableOperation.DurableOperationAsync(
                                async cancellationToken =>
                                {
                                    return await _tpmSecuredHttp.CreateHttpClientFactoryAsync(
                                        new Uri($"http://{_commandInvocationContext.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}:8790/api/node-provisioning/negotiate-certificate"),
                                        cancellationToken);
                                },
                                cancellationToken);
                            client = clientFactory.Create();
                            client.Timeout = TimeSpan.FromSeconds(5);
                            continue;
                        }
                    }
                }
                finally
                {
                    client.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_task != null)
            {
                try
                {
                    await _task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
                _task = null;
            }

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
