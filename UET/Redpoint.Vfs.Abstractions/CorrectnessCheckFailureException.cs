namespace Redpoint.Vfs.Abstractions
{
    using System;

    public class CorrectnessCheckFailureException : Exception
    {
        public CorrectnessCheckFailureException(string? message) : base(message)
        {
        }
    }
}
