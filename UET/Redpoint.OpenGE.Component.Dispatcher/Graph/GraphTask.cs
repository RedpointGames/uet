namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;

    internal class GraphTask
    {
        public required GraphTaskSpec GraphTaskSpec { get; init; }
        public required ITaskDescriptorFactory TaskDescriptorFactory { get; init; }
    }
}
