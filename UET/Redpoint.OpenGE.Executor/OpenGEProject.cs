namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.Executor.BuildSetData;

    internal class OpenGEProject
    {
        public required BuildSetProject BuildSetProject { get; init; }

        public OpenGEStatus Status { get; set; } = OpenGEStatus.Running;
    }
}
