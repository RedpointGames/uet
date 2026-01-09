namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RebootProvisioningStep : IProvisioningStep<RebootProvisioningStepConfig>
    {
        private readonly ILogger<RebootProvisioningStep> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IReboot _reboot;

        public RebootProvisioningStep(
            ILogger<RebootProvisioningStep> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IReboot reboot)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _reboot = reboot;
        }

        public string Type => "reboot";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).RebootProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags =>
            ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion |
            ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched |
            ProvisioningStepFlags.SetAsRebootStepIndex;

        public Task ExecuteOnServerBeforeAsync(
            RebootProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            // Nothing to do before this step runs.
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(
            RebootProvisioningStepConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            if (context.IsLocalTesting)
            {
                // Fetch autoexec.ipxe from server to trigger completion.
                var autoexecScript = await context.ProvisioningApiClient.GetStringAsync(
                    new Uri($"{context.ProvisioningApiEndpointHttps}/autoexec.ipxe"),
                    cancellationToken);
                _logger.LogInformation($"Fetched ipxe script instead of rebooting: {autoexecScript}");
            }
            else
            {
                // Reboot the machine.
                await _reboot.RebootMachine(cancellationToken);
            }
        }

        public Task ExecuteOnServerAfterAsync(
            RebootProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            // Nothing to do after this step runs.
            return Task.CompletedTask;
        }

        public Task<string?> GetIpxeAutoexecScriptOverrideOnServerAsync(
            RebootProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            if (config.OnceViaNotify && (nodeStatus?.Provisioner?.RebootNotificationForOnceViaNotifyOccurred ?? false))
            {
                // We have run, no longer override the script.
                return Task.FromResult<string?>(null);
            }

            if (config.DefaultInitrd)
            {
                // Script intentionally wants to reboot into the default initrd.
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(config.IpxeScriptTemplate);
        }
    }
}
