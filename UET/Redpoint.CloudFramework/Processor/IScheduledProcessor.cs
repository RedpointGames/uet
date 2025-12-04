namespace Redpoint.CloudFramework.Processor
{
    using Cronos;
    using System.Threading.Tasks;

    public interface IScheduledProcessor
    {
        static abstract string RoleName { get; }

        static abstract CronExpression CronExpression { get; }

        Task ExecuteAsync(CancellationToken cancellationToken);
    }
}
