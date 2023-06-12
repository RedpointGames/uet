namespace Redpoint.Vfs.Layer.Scratch
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// A factory which creates scratch virtual filesystem layer instances.
    /// </summary>
    public interface IScratchVfsLayerFactory
    {
        /// <summary>
        /// Create a scratch virtual filesystem layer, with the scratch data stored at the specified path.
        /// </summary>
        /// <param name="path">The path to store copy-on-write scratch data at.</param>
        /// <param name="nextLayer">The parent layer of the scratch layer. This layer can be read-only, as all writes will be handled by the scratch layer.</param>
        /// <param name="enableCorrectnessChecks">If true, additional correctness checks will be applied in the scratch layer at the cost of performance.</param>
        /// <returns>The new scratch virtual filesystem layer.</returns>
        IScratchVfsLayer CreateLayer(
            string path,
            IVfsLayer? nextLayer,
            bool enableCorrectnessChecks = false);
    }
}
