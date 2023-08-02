namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.Collections;
    using Redpoint.OpenGE.JobXml;

    internal record class Graph
    {
        public required IReadOnlyDictionary<string, JobProject> Projects { get; init; }
        public required IReadOnlyDictionary<string, GraphTask> Tasks { get; init; }
        public required IReadOnlyDependencyGraph<GraphTask> TaskDependencies { get; init; }
        public required IReadOnlyList<GraphTask> ImmediatelySchedulable { get; init; }
    }
}
