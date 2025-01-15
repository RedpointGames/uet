namespace Redpoint.CloudFramework.Processor
{
    using Quartz;

    internal interface IQuartzScheduledProcessorBinding
    {
        void Bind(QuartzOptions options);
    }
}
