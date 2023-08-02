namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.JobXml;

    internal class GraphProject
    {
        public required JobProject BuildSetProject { get; init; }

        public GraphStatus Status { get; set; } = GraphStatus.Running;
    }
}
