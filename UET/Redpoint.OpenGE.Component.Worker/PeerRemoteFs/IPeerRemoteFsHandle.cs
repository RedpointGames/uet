namespace Redpoint.OpenGE.Component.Worker.PeerRemoteFs
{
    using System;

    internal interface IPeerRemoteFsHandle : IAsyncDisposable
    {
        string Path { get; }
    }
}
