namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;

    /// <summary>
    /// The RKM API service starting component starts the <see cref="IControllerApiService"/>
    /// once all the prerequisites are met.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class RKMApiServiceStartingComponent : IComponent
    {
        private readonly IControllerApiService _controllerApiService;

        public RKMApiServiceStartingComponent(IControllerApiService controllerApiService)
        {
            _controllerApiService = controllerApiService;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for everything to be available to serve requests.
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);

            // Start the API service (and don't wait for it to run).
            _controllerApiService.StartApiForNodes();
        }
    }
}
