namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Google.Protobuf;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;

    internal interface ITaskDescriptorExecutor<T> where T : IMessage
    {
        IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            T descriptor,
            CancellationToken cancellationToken);
    }

}
