namespace Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics
{
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.Tasks;

    internal interface IStallMonitorFactory
    {
        IStallMonitor CreateStallMonitor(
            ITaskSchedulerScope taskSchedulerScope,
            GraphExecutionInstance instance);
    }
}
