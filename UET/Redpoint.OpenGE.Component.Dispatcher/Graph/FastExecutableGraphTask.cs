namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;

    internal class FastExecutableGraphTask : GraphTask, IRemotableGraphTask
    {
        public required ITaskDescriptorFactory TaskDescriptorFactory { get; init; }
        public IHashedToolInfo? ToolHashingResult { get; set; }
        public BlobHashingResult? BlobHashingResult { get; set; }
    }
}
