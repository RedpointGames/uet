namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    internal abstract class StepBaseNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;
        private readonly IProvisionerHasher _provisionerHasher;
        private readonly IProvisioningStateManager _provisioningStateManager;

        public StepBaseNodeProvisioningEndpoint(
            IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<StepBaseNodeProvisioningEndpoint>>();
            _provisioningSteps = serviceProvider.GetServices<IProvisioningStep>().ToDictionary(k => k.Type, v => v);
            _provisionerHasher = serviceProvider.GetRequiredService<IProvisionerHasher>();
            _provisioningStateManager = serviceProvider.GetRequiredService<IProvisioningStateManager>();
        }

        public abstract string Path { get; }

        public bool RequireNodeObjects => true;

        public virtual bool CanClearForceReprovisionFlag => true;

        private static async Task RespondWithRebootAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }

        private static async Task RespondWithMisconfiguredAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }

        private static Task RespondWithProvisionCompleteAsync(INodeProvisioningEndpointContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            // Update the state of provisioning for the node.
            switch (await _provisioningStateManager.UpdateStateAsync(context, CanClearForceReprovisionFlag))
            {
                case ProvisioningResponse.Misconfigured:
                    await RespondWithMisconfiguredAsync(context);
                    return;
                case ProvisioningResponse.Complete:
                    await RespondWithProvisionCompleteAsync(context);
                    return;
                case ProvisioningResponse.Reboot:
                    await RespondWithRebootAsync(context);
                    return;
            }

            // If this is the initial request after boot, check that the node booted into the correct environment.
            var isInitial = context.Request.Query.TryGetValue("initial", out var initial) && initial.FirstOrDefault() == "true";
            if (isInitial)
            {
                if (!context.Request.Query.TryGetValue("bootedFromStepIndex", out var bootedFromStepIndex) ||
                    string.IsNullOrWhiteSpace(bootedFromStepIndex) ||
                    int.Parse(bootedFromStepIndex!, CultureInfo.InvariantCulture) != (context.RkmNode!.Status!.Provisioner!.RebootStepIndex ?? -1))
                {
                    // The machine didn't boot with the expected autoexec.ipxe script, usually because
                    // the IP address of the machine during PXE boot and the IP address of the machine
                    // during provisioning inside initrd is different. When this happens, autoexec.ipxe isn't
                    // serving the desired script (which could result in the following provisioning steps
                    // running in the complete wrong environment).
                    //
                    // In this case, reset provisioning and force the machine to reboot.
                    _logger.LogError($"Machine should have booted from step index {(context.RkmNode!.Status!.Provisioner!.RebootStepIndex ?? -1)}, but booted from {bootedFromStepIndex} instead.");
                    if (await _provisioningStateManager.ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(context, true, CanClearForceReprovisionFlag))
                    {
                        await RespondWithRebootAsync(context);
                    }
                    return;
                }

                // Reset the "currentStepIndex" to the index after "lastStepCommittedIndex". This ensures
                // that step completion status don't carry over reboots unless explicitly committed.
                var lastStepCommittedIndex = context.RkmNode.Status.Provisioner.LastStepCommittedIndex ?? -1;
                var rebootStepIndex = context.RkmNode.Status.Provisioner.RebootStepIndex ?? -1;
                if (lastStepCommittedIndex < rebootStepIndex)
                {
                    // Last committed step must always at least be the reboot step index.
                    lastStepCommittedIndex = rebootStepIndex;
                }
                _logger.LogInformation($"Setting current step to {lastStepCommittedIndex + 1} during initial step fetch. Last committed step index is {lastStepCommittedIndex}, reboot step index is {rebootStepIndex}.");
                context.RkmNode.Status.Provisioner.CurrentStepIndex = lastStepCommittedIndex + 1;
                _provisioningStateManager.UpdateRegisteredIpAddressesForNode(context);
            }

            var currentStepIndex = context.RkmNode!.Status!.Provisioner!.CurrentStepIndex ?? 0;
            var currentStep = context.RkmNodeProvisioner!.Spec!.Steps![currentStepIndex];
            var provisioningStepImpl = _provisioningSteps[currentStep!.Type];

            var serverContext = new HttpContextProvisioningStepServerContext(context.HttpContext);

            await HandleStepRequestAsync(
                context,
                serverContext,
                currentStep,
                provisioningStepImpl);
        }

        protected abstract Task HandleStepRequestAsync(
            INodeProvisioningEndpointContext context,
            IProvisioningStepServerContext serverContext,
            RkmNodeProvisionerStep currentStep,
            IProvisioningStep provisioningStepImpl);
    }
}
