namespace Redpoint.OpenGE.Component.Dispatcher.Graph
{
    internal record class GraphExecutionEnvironment
    {
        public required string WorkingDirectory { get; set; }
        public required Dictionary<string, string> EnvironmentVariables { get; set; }
        public required long BuildStartTicks { get; set; }
    }
}
