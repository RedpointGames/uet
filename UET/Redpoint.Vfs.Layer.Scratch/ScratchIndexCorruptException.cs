namespace Redpoint.Vfs.Layer.Scratch
{
    using System;

    internal class ScratchIndexCorruptException : Exception
    {
        public ScratchIndexCorruptException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
