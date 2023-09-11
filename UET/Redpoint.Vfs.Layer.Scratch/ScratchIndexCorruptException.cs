namespace Redpoint.Vfs.Layer.Scratch
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only thrown and caught internally.")]
    internal sealed class ScratchIndexCorruptException : Exception
    {
        public ScratchIndexCorruptException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
