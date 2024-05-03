namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    using Redpoint.OpenGE.JobXml;
    using Redpoint.ProcessExecution;

    internal record class GraphTaskSpec
    {
        public required GraphExecutionEnvironment ExecutionEnvironment { get; init; }
        public required JobTask Task { get; init; }
        public required JobProject Project { get; init; }
        public required JobEnvironment Environment { get; init; }
        public required JobTool Tool { get; init; }
        public required Job Job { get; init; }
        public required IReadOnlyList<EscapedProcessArgument> Arguments { get; init; }
        public string WorkingDirectory => Task.WorkingDir ?? ExecutionEnvironment.WorkingDirectory;
    }
}
