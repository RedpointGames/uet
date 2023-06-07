namespace Redpoint.UET.Automation.TestLogger
{
    using Redpoint.ProcessExecution;
    using System.Threading;
    using System.Threading.Tasks;

    internal class AutomationLogForwarderProcessExecutorHook : IProcessExecutorHook
    {
        private readonly IAutomationLogForwarder _serverLifecycle;

        public AutomationLogForwarderProcessExecutorHook(IAutomationLogForwarder serverLifecycle)
        {
            _serverLifecycle = serverLifecycle;
        }

        public Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            var pipeName = _serverLifecycle.GetPipeName();
            if (pipeName != null)
            {
                if (processSpecification.EnvironmentVariables == null)
                {
                    processSpecification.EnvironmentVariables = new Dictionary<string, string>();
                }
                processSpecification.EnvironmentVariables["UET_AUTOMATION_LOGGER_PIPE_NAME"] = pipeName;
            }
            return Task.CompletedTask;
        }
    }
}
