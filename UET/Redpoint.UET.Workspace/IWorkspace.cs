namespace Redpoint.UET.Workspace
{
    using System;

    public interface IWorkspace : IAsyncDisposable
    {
        string Path { get; }
    }
}
