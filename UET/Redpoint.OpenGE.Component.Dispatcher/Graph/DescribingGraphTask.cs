namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.Protocol;

    internal class DescribingGraphTask : GraphTask
    {
        public required ITaskDescriptorFactory TaskDescriptorFactory { get; init; }
        public TaskDescriptor? TaskDescriptor { get; set; }
    }
}
