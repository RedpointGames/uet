namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.RuntimeJson;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class TestProvisioningStep : IProvisioningStep<TestProvisioningStepConfig>
    {
        private readonly ILogger<TestProvisioningStep> _logger;

        public TestProvisioningStep(
            ILogger<TestProvisioningStep> logger)
        {
            _logger = logger;
        }

        public string Type => "test";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).TestProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(
            TestProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"ServerBefore '{config.Value}'");
            return Task.CompletedTask;
        }

        public Task ExecuteOnClientAsync(
            TestProvisioningStepConfig config,
            IProvisioningStepClientContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Client '{config.Value}'");
            return Task.CompletedTask;
        }

        public Task ExecuteOnServerAfterAsync(
            TestProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"ServerAfter '{config.Value}'");
            return Task.CompletedTask;
        }
    }
}
