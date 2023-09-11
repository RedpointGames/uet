namespace Redpoint.Uet.BuildPipeline.Executors
{
    using System.Runtime.Serialization;

    [Serializable]
    public class BuildPipelineExecutionFailureException : Exception
    {
        public BuildPipelineExecutionFailureException()
        {
        }

        public BuildPipelineExecutionFailureException(string? message) : base(message)
        {
        }

        public BuildPipelineExecutionFailureException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BuildPipelineExecutionFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
