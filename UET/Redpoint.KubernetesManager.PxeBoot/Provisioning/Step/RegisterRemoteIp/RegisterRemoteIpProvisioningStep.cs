namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RegisterRemoteIpProvisioningStep : IProvisioningStep<EmptyProvisioningStepConfig>
    {
        private readonly ILogger<RegisterRemoteIpProvisioningStep> _logger;

        public RegisterRemoteIpProvisioningStep(
            ILogger<RegisterRemoteIpProvisioningStep> logger)
        {
            _logger = logger;
        }

        public string Type => "registerRemoteIp";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).EmptyProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(
            EmptyProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            // This step is now deprecated as registered IP addresses are automatically
            // updated whenever the node requests a reboot or calls /step with initial=true.
            _logger.LogWarning($"The '{Type}' step is deprecated and no longer does anything. Please remove it from your provisioner steps.");
            return Task.CompletedTask;
        }

        public Task ExecuteOnClientAsync(
            EmptyProvisioningStepConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            // Nothing to do on the client.
            return Task.CompletedTask;
        }

        public Task ExecuteOnServerAfterAsync(
            EmptyProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            // Nothing to do after this step runs.
            return Task.CompletedTask;
        }
    }
}
