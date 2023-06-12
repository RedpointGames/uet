namespace Redpoint.Vfs.Abstractions
{
    using System;

    /// <summary>
    /// Raised when a virtual filesystem layer would encounter a deadlock.
    /// </summary>
    public class VfsLayerDeadlockException : Exception
    {
        /// <summary>
        /// Raised when a virtual filesystem layer would encounter a deadlock.
        /// </summary>
        public VfsLayerDeadlockException(string? message) : base(message)
        {
        }
    }
}
