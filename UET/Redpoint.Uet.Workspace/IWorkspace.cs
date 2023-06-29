namespace Redpoint.Uet.Workspace
{
    using System;

    public interface IWorkspace : IAsyncDisposable
    {
        string Path { get; }
    }
}
