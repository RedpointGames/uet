namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;

    /// <summary>
    /// The node component guard component waits until all prerequisites are
    /// met before allowing a controller to start it's node-related processes.
    /// This means waiting for the API server to be up and the core networking
    /// components to be provisioned.
    /// </summary>
    internal class NodeComponentGuardComponent : IComponent
    {
        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for the prerequisites.
            await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubernetesResourcesProvisioned);
            await context.WaitForFlagAsync(WellKnownFlags.CalicoProvisioned);
            await context.WaitForFlagAsync(WellKnownFlags.CoreDNSProvisioned);

            // Now we're ready to start our node-related components. We don't get the translated name
            // here because the WSL components automatically take into account the suffixed name.
            context.SetFlag(WellKnownFlags.NodeComponentsReadyToStart, new NodeNameContextData(Environment.MachineName.ToLowerInvariant()));
        }
    }
}
