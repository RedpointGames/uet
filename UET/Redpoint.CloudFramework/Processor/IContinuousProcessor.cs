namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Threading.Tasks;

    public interface IContinuousProcessor : IProcessor
    {
        Task ExecuteAsync(CancellationToken shutdownCancellationToken);
    }
}
