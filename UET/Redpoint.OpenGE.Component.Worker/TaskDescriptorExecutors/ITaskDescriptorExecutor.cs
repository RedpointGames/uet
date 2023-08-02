namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Google.Protobuf;
    using System.Collections.Generic;

    internal interface ITaskDescriptorExecutor<T> where T : IMessage
    {
        IAsyncEnumerable<Protocol.ProcessResponse> ExecuteAsync(
            T descriptor,
            CancellationToken cancellationToken);
    }

}
