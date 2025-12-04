namespace Redpoint.CloudFramework.Processor
{
    using Quartz;
    using System.Threading.Tasks;

    public interface IScheduledProcessor : IProcessor
    {
        static abstract void ConfigureSchedule(TriggerBuilder trigger);

        Task ExecuteAsync(IJobExecutionContext context);
    }
}
