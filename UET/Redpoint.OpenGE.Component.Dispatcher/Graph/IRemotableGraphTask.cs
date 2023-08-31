namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;

    internal interface IRemotableGraphTask
    {
        IHashedToolInfo? ToolHashingResult { get; }
        BlobHashingResult? BlobHashingResult { get; }
    }
}
