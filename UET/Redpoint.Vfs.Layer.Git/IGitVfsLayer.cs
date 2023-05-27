namespace Redpoint.Vfs.Layer.Git
{
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IGitVfsLayer : IVfsLayer
    {
        Task InitAsync(CancellationToken cancellationToken);

        IReadOnlyDictionary<string, string> Files { get; }

        DateTimeOffset Created { get; }
    }
}
