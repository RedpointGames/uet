namespace Redpoint.UET.BuildPipeline.Executors
{
    using System.Runtime.Serialization;

    [Serializable]
    public class BuildPipelineExecutionFailure : Exception
    {
        public BuildPipelineExecutionFailure()
        {
        }

        public BuildPipelineExecutionFailure(string? message) : base(message)
        {
        }

        public BuildPipelineExecutionFailure(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BuildPipelineExecutionFailure(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
