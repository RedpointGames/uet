namespace Redpoint.Vfs.Layer.Scratch
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Additional virtual filesystem layer APIs that are specific to the scratch layer.
    /// </summary>
    public interface IScratchVfsLayer : IVfsLayer
    {
        /// <summary>
        /// Returns whether the virtual filesystem path exists and what type of materialization currently applies in the scratch layer.
        /// </summary>
        /// <param name="path">The path in the virtual filesystem.</param>
        /// <returns>The materialization status and virtual filesystem entry existence status of the path.</returns>
        (ScratchVfsPathStatus status, VfsEntryExistence existence) GetPathStatus(string path);
    }
}
