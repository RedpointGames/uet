namespace Redpoint.Vfs.Layer.Scratch
{
    /// <summary>
    /// The scratch status of a path in the virtual filesystem layer.
    /// </summary>
    public enum ScratchVfsPathStatus
    {
        /// <summary>
        /// The file exists in the copy-on-write scratch area.
        /// </summary>
        Materialized,

        /// <summary>
        /// The file exists only in the parent layer (and no writes have been made to this file yet).
        /// </summary>
        Passthrough,

        /// <summary>
        /// The file does not exist, and does not exist in the parent layer.
        /// </summary>
        Nonexistent,

        /// <summary>
        /// The file exists in the parent layer, but has been marked as deleted in the scratch area.
        /// </summary>
        NonexistentTombstoned,
    }
}
