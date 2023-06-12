namespace Redpoint.Vfs.Abstractions
{
    using System;

    /// <summary>
    /// Raised when a virtual filesystem layer detects that it would produce an incorrect result due to a lower layer or invalid cache.
    /// </summary>
    public class CorrectnessCheckFailureException : Exception
    {
        /// <summary>
        /// Raised when a virtual filesystem layer detects that it would produce an incorrect result due to a lower layer or invalid cache.
        /// </summary>
        public CorrectnessCheckFailureException(string? message) : base(message)
        {
        }
    }
}
