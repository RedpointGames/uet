namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Sequence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class AtomicSequenceProvisioningStep : IProvisioningStep<AtomicSequenceProvisioningStepConfig>
    {
        private readonly ILogger<AtomicSequenceProvisioningStep> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AtomicSequenceProvisioningStep(
            ILogger<AtomicSequenceProvisioningStep> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public string Type => "atomicSequence";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).AtomicSequenceProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(AtomicSequenceProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(AtomicSequenceProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting atomic sequence of steps...");

            // @note: We need to get services here and not the constructor, otherwise it's
            // a recursive dependency.
            var provisioningSteps = _serviceProvider
                .GetServices<IProvisioningStep>()
                .ToList();

            foreach (var currentStep in config.Steps ?? [])
            {
                if (currentStep == null)
                {
                    continue;
                }

                _logger.LogInformation($"Starting step '{currentStep.Type}'...");
                var provisioningStep = provisioningSteps.FirstOrDefault(x => string.Equals(x.Type, currentStep?.Type, StringComparison.OrdinalIgnoreCase));
                if (provisioningStep == null)
                {
                    throw new UnableToProvisionSystemException($"Step inside atomic sequence with type '{currentStep.Type}' is not known.");
                }

                if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DisallowInAtomicSequence))
                {
                    throw new UnableToProvisionSystemException($"Step with type '{currentStep.Type}' is not permitted in atomic sequence, because it requires server events to execute which do not occur for steps in an atomic sequence.");
                }

                await provisioningStep.ExecuteOnClientUncastedAsync(
                    currentStep?.DynamicSettings,
                    context,
                    cancellationToken);
            }

            _logger.LogInformation("Atomic sequence completed successfully.");
        }

        public Task ExecuteOnServerAfterAsync(AtomicSequenceProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
