namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution.Enumerable;
    using ProcessResponse = ProcessExecution.Enumerable.ProcessResponse;

    internal class DefaultProcessExecutorResponseConverter : IProcessExecutorResponseConverter
    {
        private readonly ILogger<DefaultProcessExecutorResponseConverter> _logger;

        public DefaultProcessExecutorResponseConverter(ILogger<DefaultProcessExecutorResponseConverter> logger)
        {
            _logger = logger;
        }

        public ExecuteTaskResponse ConvertResponse(ProcessResponse response)
        {
            switch (response)
            {
                case ExitCodeResponse exitCode:
                    return new ExecuteTaskResponse
                    {
                        Response = new Protocol.ProcessResponse
                        {
                            ExitCode = exitCode.ExitCode,
                        },
                    };
                case StandardOutputResponse standardOutput:
                    _logger.LogTrace(standardOutput.Data);
                    return new ExecuteTaskResponse
                    {
                        Response = new Protocol.ProcessResponse
                        {
                            StandardOutputLine = standardOutput.Data,
                        },
                    };
                case StandardErrorResponse standardError:
                    _logger.LogTrace(standardError.Data);
                    return new ExecuteTaskResponse
                    {
                        Response = new Protocol.ProcessResponse
                        {
                            StandardErrorLine = standardError.Data,
                        },
                    };
                default:
                    throw new InvalidOperationException("Received unexpected ProcessResponse type from IProcessExecutor!");
            };
        }
    }
}
