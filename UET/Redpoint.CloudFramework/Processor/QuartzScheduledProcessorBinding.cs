namespace Redpoint.CloudFramework.Processor
{
    using Quartz;

    internal class QuartzScheduledProcessorBinding<T> : IQuartzScheduledProcessorBinding where T : IScheduledProcessor
    {
        private readonly string _datastoreIdentifier;
        private readonly Action<TriggerBuilder> _triggerBuilder;

        public QuartzScheduledProcessorBinding(
            string datastoreIdentifier,
            Action<TriggerBuilder> triggerBuilder)
        {
            _datastoreIdentifier = datastoreIdentifier;
            _triggerBuilder = triggerBuilder;
        }

        public void Bind(QuartzOptions options)
        {
            options.AddJob<QuartzJobMapping<T>>(configure =>
            {
                configure.DisallowConcurrentExecution(true)
                    .PersistJobDataAfterExecution(false)
                    .RequestRecovery(false)
                    .StoreDurably(false)
                    .WithIdentity(_datastoreIdentifier);
            });
            options.AddTrigger(configure =>
            {
                _triggerBuilder(configure);
                configure.ForJob(_datastoreIdentifier)
                    .WithIdentity(_datastoreIdentifier);
            });
        }
    }
}
