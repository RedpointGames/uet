namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class InitializeOsPartitionProvisioningStep : IProvisioningStep<InitializeOsPartitionProvisioningStepConfig>
    {
        private readonly ILogger<InitializeOsPartitionProvisioningStep> _logger;
        private readonly IOperatingSystemPartitionManager _operatingSystemPartitionManager;

        public InitializeOsPartitionProvisioningStep(
            ILogger<InitializeOsPartitionProvisioningStep> logger,
            IOperatingSystemPartitionManager operatingSystemPartitionManager)
        {
            _logger = logger;
            _operatingSystemPartitionManager = operatingSystemPartitionManager;
        }

        public string Type => "initializeOsPartition";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).InitializeOsPartitionProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(InitializeOsPartitionProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(InitializeOsPartitionProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            if (context.DiskPathLinux == null)
            {
                throw new UnableToProvisionSystemException("context.DiskPathLinux is null; initializeOsPartition can only be used when running on Linux.");
            }

            await _operatingSystemPartitionManager.InitializeOperatingSystemDiskAsync(
                context.DiskPathLinux,
                config.Filesystem,
                cancellationToken);

            await _operatingSystemPartitionManager.TryMountOperatingSystemDiskAsync(
                context.DiskPathLinux,
                cancellationToken);
        }

        public Task ExecuteOnServerAfterAsync(InitializeOsPartitionProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
