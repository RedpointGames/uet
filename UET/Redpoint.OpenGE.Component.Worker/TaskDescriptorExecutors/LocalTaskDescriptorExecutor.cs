namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal class LocalTaskDescriptorExecutor : ITaskDescriptorExecutor<LocalTaskDescriptor>
    {
        private readonly IProcessExecutor _processExecutor;

        public LocalTaskDescriptorExecutor(
            IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        public async IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            LocalTaskDescriptor descriptor, 
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var response in _processExecutor.ExecuteAsync(new ProcessSpecification
                {
                    FilePath = descriptor.Path,
                    Arguments = descriptor.Arguments,
                    EnvironmentVariables = descriptor.EnvironmentVariables.Count > 0 
                        ? descriptor.EnvironmentVariables 
                        : null,
                    WorkingDirectory = descriptor.WorkingDirectory,
                },
                cancellationToken))
            {
                yield return new ExecuteTaskResponse
                {
                    Response = response switch
                    {
                        ExitCodeResponse r => new Protocol.ProcessResponse
                        {
                            ExitCode = r.ExitCode,
                        },
                        StandardOutputResponse r => new Protocol.ProcessResponse
                        {
                            StandardOutputLine = r.Data,
                        },
                        StandardErrorResponse r => new Protocol.ProcessResponse
                        {
                            StandardOutputLine = r.Data,
                        },
                        _ => throw new InvalidOperationException("Received unexpected ProcessResponse type from IProcessExecutor!"),
                    }
                };
            }
        }
    }

}
