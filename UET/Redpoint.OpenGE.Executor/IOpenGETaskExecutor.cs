namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.Executor.BuildSetData;
    using System;
    using System.Threading.Tasks;

    internal interface IOpenGETaskExecutor
    {
        int ScoreTask(
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments);

        Task<IDisposable> AllocateVirtualCoreForTaskExecutionAsync(
            CancellationToken cancellationToken);

        Task<int> ExecuteTaskAsync(
            IDisposable virtualCore,
            string buildStatusLogPrefix,
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments,
            Dictionary<string, string> globalEnvironmentVariables,
            Action<string> onStandardOutputLine,
            Action<string> onStandardErrorLine,
            CancellationToken cancellationToken);
    }
}
