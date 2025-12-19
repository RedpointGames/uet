namespace UET.Commands.Cluster
{
    using System.Threading.Tasks;

    public interface IHostedServiceFromExecutable
    {
        Task RunHostedServicesAsync(CancellationToken cancellationToken);
    }
}
