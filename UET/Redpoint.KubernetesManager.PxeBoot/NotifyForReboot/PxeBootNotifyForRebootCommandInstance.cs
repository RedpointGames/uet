namespace Redpoint.KubernetesManager.PxeBoot.NotifyForReboot
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using System;
    using System.Threading.Tasks;
    using System.Web;

    internal class PxeBootNotifyForRebootCommandInstance : ICommandInstance
    {
        private readonly IProvisionContextDiscoverer _provisionContextDiscoverer;
        private readonly IDurableOperation _durableOperation;
        private readonly ILogger<PxeBootNotifyForRebootCommandInstance> _logger;
        private readonly PxeBootNotifyForRebootOptions _options;

        public PxeBootNotifyForRebootCommandInstance(
            IProvisionContextDiscoverer provisionContextDiscoverer,
            IDurableOperation durableOperation,
            ILogger<PxeBootNotifyForRebootCommandInstance> logger,
            PxeBootNotifyForRebootOptions options)
        {
            _provisionContextDiscoverer = provisionContextDiscoverer;
            _durableOperation = durableOperation;
            _logger = logger;
            _options = options;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            // Get the provisioning context, just for the API address server.
            var provisionContext = await _provisionContextDiscoverer.GetProvisionContextAsync(
                false,
                context.GetCancellationToken());

            // Notify on an unauthenticated endpoint. This command needs to run in very limited WinPE
            // environments that can not use the TPM yet.
            await _durableOperation.DurableOperationAsync(
                async cancellationToken =>
                {
                    using var client = new HttpClient();
                    var response = await client.GetAsync(
                        new Uri($"http://{provisionContext.ApiAddress}:8790/notify-for-reboot?fingerprint={HttpUtility.UrlEncode(context.ParseResult.GetValueForOption(_options.Fingerprint))}"),
                        cancellationToken);
                    response.EnsureSuccessStatusCode();
                },
                context.GetCancellationToken());
            _logger.LogInformation("Notified server that we have completed our once-only reboot.");

            // We're done.
            return 0;
        }
    }
}
