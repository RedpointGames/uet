namespace Redpoint.Uet.Automation.TestLogger
{
    using Redpoint.ProcessExecution;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AutomationLogForwarderProcessExecutorHook : IProcessExecutorHook
    {
        private readonly IAutomationLogForwarder _serverLifecycle;

        public AutomationLogForwarderProcessExecutorHook(IAutomationLogForwarder serverLifecycle)
        {
            _serverLifecycle = serverLifecycle;
        }

        public Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            if (AutomationLoggerPipe.AllowLoggerPipe)
            {
                var pipeName = _serverLifecycle.GetPipeName();
                if (pipeName != null)
                {
                    var newEnvironmentVariables = (processSpecification.EnvironmentVariables != null) ? new Dictionary<string, string>(processSpecification.EnvironmentVariables) : new Dictionary<string, string>();
                    newEnvironmentVariables["UET_AUTOMATION_LOGGER_PIPE_NAME"] = pipeName;
                    processSpecification.EnvironmentVariables = newEnvironmentVariables;
                }
            }

            return Task.CompletedTask;
        }
    }
}
