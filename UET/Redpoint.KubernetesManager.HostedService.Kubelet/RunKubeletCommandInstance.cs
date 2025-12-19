namespace UET.Commands.Cluster
{
    using Redpoint.CommandLine;
    using System.Threading.Tasks;

    internal sealed class RunKubeletCommandInstance : ICommandInstance
    {
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

        public RunKubeletCommandInstance(
            IHostedServiceFromExecutable hostedServiceFromExecutable)
        {
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            // Store the invocation context so that we can get the command line arguments inside the hosted service.
            KubeletHostedService.InvocationContext = context;

            await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
            return 0;
        }
    }
}
