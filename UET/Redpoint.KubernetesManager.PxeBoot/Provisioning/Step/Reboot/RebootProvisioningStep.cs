namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RebootProvisioningStep : IProvisioningStep<RebootProvisioningStepConfig>
    {
        private readonly ILogger<RebootProvisioningStep> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        public RebootProvisioningStep(
            ILogger<RebootProvisioningStep> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        public string Type => "reboot";

        public IRuntimeJson Settings => new ProvisioningStepConfigRuntimeJson(ProvisioningStepConfigJsonSerializerContext.WithStringEnum).RebootProvisioningStepConfig;

        public ProvisioningStepFlags Flags =>
            ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion |
            ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched;

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
                    new Uri($"{context.ProvisioningApiEndpoint}/autoexec.ipxe"),
                    cancellationToken);
                _logger.LogInformation($"Fetched ipxe script instead of rebooting: {autoexecScript}");
            }
            else
            {
                // Reboot the machine.
                if (OperatingSystem.IsWindows())
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("shutdown.exe"),
                            Arguments = ["/g", "/t", "0", "/c", "RKM Provisioning", "/f", "/d", "p:4:1"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("shutdown"),
                            Arguments = ["-r", "now"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                else if (OperatingSystem.IsLinux())
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("systemctl"),
                            Arguments = ["--message=\"RKM Provisioning\"", "reboot"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                // Sleep indefinitely until the machine reboots.
                await Task.Delay(-1, cancellationToken);
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
            return Task.FromResult<string?>(config.IpxeScriptTemplate);
        }
    }
}
