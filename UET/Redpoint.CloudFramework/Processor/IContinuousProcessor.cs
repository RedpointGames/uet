namespace Redpoint.CloudFramework.Processor
{
    using System.Threading.Tasks;

    public interface IContinuousProcessor
    {
        static abstract string RoleName { get; }

        Task ExecuteAsync(CancellationToken shutdownCancellationToken);
    }
}
