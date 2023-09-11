namespace UET.Commands.Build
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
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

        protected BuildMisconfigurationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}