namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.Executor.BuildSetData;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class OpenGETask
    {
        public required BuildSetTask BuildSetTask { get; init; }

        public required BuildSetProject BuildSetProject { get; init; }

        public required BuildSet BuildSet { get; init; }
        public List<OpenGETask> DependsOn { get; init; } = new List<OpenGETask>();
        public List<OpenGETask> Dependents { get; init; } = new List<OpenGETask>();

        public Task? ExecutingTask { get; set; } = null;

        public OpenGEStatus Status { get; set; } = OpenGEStatus.Pending;
    }
}
