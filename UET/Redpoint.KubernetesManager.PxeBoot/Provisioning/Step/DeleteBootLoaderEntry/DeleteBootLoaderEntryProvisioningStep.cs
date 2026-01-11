namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.DeleteBootLoaderEntry
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Bootmgr;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DeleteBootLoaderEntryProvisioningStep : IProvisioningStep<DeleteBootLoaderEntryProvisioningStepConfig>
    {
        private readonly ILogger<DeleteBootLoaderEntryProvisioningStep> _logger;
        private readonly IEfiBootManager? _efiBootManager;

        public DeleteBootLoaderEntryProvisioningStep(
            ILogger<DeleteBootLoaderEntryProvisioningStep> logger,
            IEfiBootManager? efiBootManager = null)
        {
            _logger = logger;
            // @note: This is optional because it's not available on the server.
            _efiBootManager = efiBootManager;
        }

        public string Type => "deleteBootLoaderEntry";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).DeleteBootLoaderEntryProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(DeleteBootLoaderEntryProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(DeleteBootLoaderEntryProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            if (_efiBootManager == null)
            {
                throw new UnableToProvisionSystemException("EFI boot manager was not available (unexpected bug).");
            }

            var configuration = await _efiBootManager.GetBootManagerConfigurationAsync(cancellationToken);

            foreach (var entry in configuration.BootEntries)
            {
                if (string.Equals(entry.Value.Name, config.Name, StringComparison.Ordinal))
                {
                    _logger.LogInformation($"Removing boot loader entry {entry.Key} '{entry.Value.Name}'...");
                    await _efiBootManager.RemoveBootManagerEntryAsync(entry.Key, cancellationToken);
                }
            }
        }

        public Task ExecuteOnServerAfterAsync(DeleteBootLoaderEntryProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
