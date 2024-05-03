namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Google.Protobuf.Collections;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.CompilerServices;

    internal class LocalTaskDescriptorExecutor : ITaskDescriptorExecutor<LocalTaskDescriptor>
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly IProcessExecutorResponseConverter _processExecutorResponseConverter;

        public LocalTaskDescriptorExecutor(
            IProcessExecutor processExecutor,
            IProcessExecutorResponseConverter processExecutorResponseConverter)
        {
            _processExecutor = processExecutor;
            _processExecutorResponseConverter = processExecutorResponseConverter;
        }

        private static IEnumerable<LogicalProcessArgument> ConvertArguments(RepeatedField<ProcessArgument> arguments)
        {
            return arguments.Select(x => string.IsNullOrWhiteSpace(x.OriginalValue) ? new LogicalProcessArgument(x.LogicalValue) : new EscapedProcessArgument(x.LogicalValue, x.OriginalValue));
        }

        public async IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            IPAddress peerAddress,
            LocalTaskDescriptor descriptor,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (descriptor.Path == "__openge_unit_testing__")
            {
                yield return new ExecuteTaskResponse
                {
                    Response = new Protocol.ProcessResponse
                    {
                        ExitCode = 0,
                    }
                };
                yield break;
            }

            await foreach (var response in _processExecutor.ExecuteAsync(new ProcessSpecification
            {
                FilePath = descriptor.Path,
                Arguments = ConvertArguments(descriptor.Arguments),
                EnvironmentVariables = descriptor.EnvironmentVariables.Count > 0
                                    ? descriptor.EnvironmentVariables
                                    : null,
                WorkingDirectory = descriptor.WorkingDirectory,
            },
            cancellationToken))
            {
                yield return _processExecutorResponseConverter.ConvertResponse(response);
            }
        }
    }

}
