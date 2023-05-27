namespace Redpoint.Vfs.Abstractions
{
    using System;

    public class ScratchLayerDeadlockException : Exception
    {
        public ScratchLayerDeadlockException(string? message) : base(message)
        {
        }
    }
}
