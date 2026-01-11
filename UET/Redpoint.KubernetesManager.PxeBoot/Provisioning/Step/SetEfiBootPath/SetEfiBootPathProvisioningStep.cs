namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetEfiBootPath
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SetEfiBootPathProvisioningStep : IProvisioningStep<SetEfiBootPathProvisioningStepConfig>
    {
        private readonly ILogger<SetEfiBootPathProvisioningStep> _logger;

        public SetEfiBootPathProvisioningStep(
            ILogger<SetEfiBootPathProvisioningStep> logger)
        {
            _logger = logger;
        }

        public string Type => "setEfiBootPath";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).SetEfiBootPathProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.DisallowInAtomicSequence;

        public Task ExecuteOnServerBeforeAsync(SetEfiBootPathProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Boot EFI path set to: {config.Path}");
            nodeStatus.BootEfiPath = config.Path;
            return Task.CompletedTask;
        }

        public Task ExecuteOnClientAsync(SetEfiBootPathProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteOnServerAfterAsync(SetEfiBootPathProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
