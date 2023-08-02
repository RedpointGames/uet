namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.JobXml;
    using System;
    using System.Threading.Tasks;

    internal interface ITaskExecutor
    {
        int ScoreTask(
            OpenGETask task,
            JobEnvironment environment,
            JobTool tool,
            string[] arguments);

        Task<IDisposable> AllocateVirtualCoreForTaskExecutionAsync(
            CancellationToken cancellationToken);

        Task<int> ExecuteTaskAsync(
            IDisposable virtualCore,
            string buildStatusLogPrefix,
            OpenGETask task,
            JobEnvironment environment,
            JobTool tool,
            string[] arguments,
            Dictionary<string, string> globalEnvironmentVariables,
            Action<string> onStandardOutputLine,
            Action<string> onStandardErrorLine,
            CancellationToken cancellationToken);
    }
}
