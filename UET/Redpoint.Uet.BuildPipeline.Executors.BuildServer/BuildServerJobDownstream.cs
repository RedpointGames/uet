namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    public record class BuildServerJobDownstream
    {
        public required string DownstreamProject { get; set; }

        public required string DownstreamBranch { get; set; }

        public required string DownstreamDistribution { get; set; }
    }
}
