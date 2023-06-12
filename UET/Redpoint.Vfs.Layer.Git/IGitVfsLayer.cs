namespace Redpoint.Vfs.Layer.Git
{
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Additional virtual filesystem layer APIs that are specific to the Git layer.
    /// </summary>
    public interface IGitVfsLayer : IVfsLayer
    {
        /// <summary>
        /// Initialize the Git layer. You must call this before the layer is used with a virtual filesystem driver.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task.</returns>
        Task InitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// A list of all files in the Git layer.
        /// </summary>
        IReadOnlyDictionary<string, string> Files { get; }

        /// <summary>
        /// The date at which the Git commit was created.
        /// </summary>
        DateTimeOffset Created { get; }
    }
}
