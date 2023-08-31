namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.Protocol;

    internal class DescribingGraphTask : GraphTask, IRemotableGraphTask
    {
        public required ITaskDescriptorFactory TaskDescriptorFactory { get; init; }
        public TaskDescriptor? TaskDescriptor { get; set; }
        public IHashedToolInfo? ToolHashingResult { get; set; }
        public BlobHashingResult? BlobHashingResult { get; set; }
    }
}
