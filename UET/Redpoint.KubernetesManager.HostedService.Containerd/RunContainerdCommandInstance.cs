namespace Redpoint.KubernetesManager.HostedService.Containerd
{
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using System.Threading.Tasks;

    internal sealed class RunContainerdCommandInstance : ICommandInstance
    {
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

        public RunContainerdCommandInstance(
            IHostedServiceFromExecutable hostedServiceFromExecutable)
        {
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            // Store the invocation context so that we can get the command line arguments inside the hosted service.
            ContainerdHostedService.InvocationContext = context;

            await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
            return 0;
        }
    }
}
