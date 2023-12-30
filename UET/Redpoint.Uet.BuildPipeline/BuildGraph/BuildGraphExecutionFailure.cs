namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;

    [Serializable]
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only used internally.")]
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
    }
}