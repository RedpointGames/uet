namespace Redpoint.Uet.Commands.Build
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;

    public sealed class BuildMisconfigurationException : Exception
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