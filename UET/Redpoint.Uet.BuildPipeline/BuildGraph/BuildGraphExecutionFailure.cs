namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class BuildGraphExecutionFailure : Exception
    {
        public BuildGraphExecutionFailure()
        {
        }

        public BuildGraphExecutionFailure(string? message) : base(message)
        {
        }

        public BuildGraphExecutionFailure(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BuildGraphExecutionFailure(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}