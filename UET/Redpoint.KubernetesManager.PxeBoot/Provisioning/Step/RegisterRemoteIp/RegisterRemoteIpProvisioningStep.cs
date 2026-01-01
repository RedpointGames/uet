namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
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

        public IRuntimeJson Settings => new ProvisioningStepConfigRuntimeJson(ProvisioningStepConfigJsonSerializerContext.WithStringEnum).EmptyProvisioningStepConfig;

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(
            EmptyProvisioningStepConfig config,
            RkmNodeStatus nodeStatus,
            IProvisioningStepServerContext serverContext,
            CancellationToken cancellationToken)
        {
            var threshold = DateTimeOffset.UtcNow;
            var newExpiry = DateTimeOffset.UtcNow.AddDays(1);

            var addresses = new List<IPAddress>
            {
                serverContext.RemoteIpAddress
            };
            if (serverContext.RemoteIpAddress.IsIPv4MappedToIPv6)
            {
                addresses.Add(serverContext.RemoteIpAddress.MapToIPv4());
            }

            nodeStatus.RegisteredIpAddresses ??= new List<RkmNodeStatusRegisteredIpAddress>();
            nodeStatus.RegisteredIpAddresses.RemoveAll(x => !x.ExpiresAt.HasValue || x.ExpiresAt.Value < threshold);

            foreach (var addressRaw in addresses)
            {
                var address = addressRaw.ToString();

                var existingEntry = nodeStatus.RegisteredIpAddresses.FirstOrDefault(x => x.Address == address);
                if (existingEntry != null)
                {
                    _logger.LogInformation($"Updating existing expiry of registered IP address '{address}' to {newExpiry}...");
                    existingEntry.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
                }
                else
                {
                    _logger.LogInformation($"Adding new entry for registered IP address '{address}' with expiry {newExpiry}...");
                    nodeStatus.RegisteredIpAddresses.Add(new RkmNodeStatusRegisteredIpAddress
                    {
                        Address = address,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                    });
                }
            }

            return Task.CompletedTask;
        }

        public Task ExecuteOnClientAsync(
            EmptyProvisioningStepConfig config,
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
