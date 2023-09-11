namespace Redpoint.Vfs.Layer.Scratch
{
    using System;

    internal sealed class ScratchIndexCorruptException : Exception
    {
        public ScratchIndexCorruptException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
