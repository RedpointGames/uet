namespace BuildRunner.Workspace
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal interface IWorkspace : IAsyncDisposable
    {
        string Path { get; }
    }
}
