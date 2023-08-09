namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;

    internal class ExecutableGraphTask : GraphTask
    {
        public required DescribingGraphTask DescribingGraphTask { get; init; }
    }

    internal class FastExecutableGraphTask : GraphTask
    {
        public required ITaskDescriptorFactory TaskDescriptorFactory { get; init; }
    }
}
