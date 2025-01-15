namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.Options;
    using Quartz;

    internal class QuartzCloudFrameworkPostConfigureOptions : IPostConfigureOptions<QuartzOptions>
    {
        private readonly IEnumerable<IQuartzScheduledProcessorBinding> _bindings;

        public QuartzCloudFrameworkPostConfigureOptions(
            IEnumerable<IQuartzScheduledProcessorBinding> bindings)
        {
            _bindings = bindings;
        }

        public void PostConfigure(string? name, QuartzOptions options)
        {
            foreach (var binding in _bindings)
            {
                binding.Bind(options);
            }
        }
    }
}
