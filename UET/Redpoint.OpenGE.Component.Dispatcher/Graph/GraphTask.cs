namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class GraphTask
    {
        public required GraphTaskSpec GraphTaskSpec { get; init; }
        public required TaskDescriptor TaskDescriptor { get; init; }

        public List<GraphTask> DependsOn { get; init; } = new List<GraphTask>();
        public List<GraphTask> Dependents { get; init; } = new List<GraphTask>();

        public Task? ExecutingTask { get; set; } = null;

        public GraphStatus Status { get; set; } = GraphStatus.Pending;
    }
}
