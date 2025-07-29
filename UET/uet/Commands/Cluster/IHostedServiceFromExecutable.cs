namespace UET.Commands.Cluster
{
    using System.Threading.Tasks;

    internal interface IHostedServiceFromExecutable
    {
        Task RunHostedServicesAsync(CancellationToken cancellationToken);
    }
}
