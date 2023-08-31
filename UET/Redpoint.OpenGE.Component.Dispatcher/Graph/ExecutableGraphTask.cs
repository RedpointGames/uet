namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;

    internal class ExecutableGraphTask : GraphTask, IRemotableGraphTask
    {
        public required DescribingGraphTask DescribingGraphTask { get; init; }

        public IHashedToolInfo? ToolHashingResult => DescribingGraphTask.ToolHashingResult;

        public BlobHashingResult? BlobHashingResult => DescribingGraphTask.BlobHashingResult;
    }
}
