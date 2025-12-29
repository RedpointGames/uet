namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RecoveryShell
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RecoveryShellProvisioningStep : IProvisioningStep<EmptyProvisioningStepConfig>
    {
        private readonly ILogger<RecoveryShellProvisioningStep> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;

        public RecoveryShellProvisioningStep(
            ILogger<RecoveryShellProvisioningStep> logger,
            IProcessExecutor processExecutor,
            IPathResolver pathResolver)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
        }

        public string Type => "recoveryShell";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).EmptyProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(EmptyProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            // Nothing to do before this step runs.
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(EmptyProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Starting recovery shell as requested by the provisioner...");
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath(OperatingSystem.IsWindows() ? "powershell.exe" : "bash"),
                    Arguments = []
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
        }

        public Task ExecuteOnServerAfterAsync(EmptyProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            // Nothing to do after this step runs.
            return Task.CompletedTask;
        }
    }
}
