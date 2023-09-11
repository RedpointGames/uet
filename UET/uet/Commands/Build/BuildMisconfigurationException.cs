namespace UET.Commands.Build
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;

    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only used internally.")]
    internal sealed class BuildMisconfigurationException : Exception
    {
        public BuildMisconfigurationException()
        {
        }

        public BuildMisconfigurationException(string? message) : base(message)
        {
        }

        public BuildMisconfigurationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}