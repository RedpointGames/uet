namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RegisterRemoteIp
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RebootProvisioningStep : IProvisioningStep<RebootProvisioningStepConfig>
    {
        private readonly ILogger<RebootProvisioningStep> _logger;

        public RebootProvisioningStep(
            ILogger<RebootProvisioningStep> logger)
        {
            _logger = logger;
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

        public Task ExecuteOnClientAsync(
            RebootProvisioningStepConfig config,
            CancellationToken cancellationToken)
        {
            // Nothing to do on the client.
            return Task.CompletedTask;
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
