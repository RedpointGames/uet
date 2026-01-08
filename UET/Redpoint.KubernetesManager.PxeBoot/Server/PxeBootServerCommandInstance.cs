namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using System.Threading.Tasks;

    internal class PxeBootServerCommandInstance : ICommandInstance
    {
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

        public PxeBootServerCommandInstance(
            IHostedServiceFromExecutable hostedServiceFromExecutable)
        {
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
            return 0;
        }
    }
}
