namespace Redpoint.CloudFramework.Processor
{
    using Quartz;
    using System.Threading.Tasks;

    public interface IScheduledProcessor
    {
        static abstract string RoleName { get; }

        Task ExecuteAsync(IJobExecutionContext context);
    }
}
