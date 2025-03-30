namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    public class JenkinsJob
    {
        public JenkinsJobStatus Status { get; set; } = JenkinsJobStatus.Idle;

        public Uri? QueueUri { get; set; }

        public Uri? ExecutionUri { get; set; }

        public int ExecutionLogByteOffset { get; set; }
    }
}
