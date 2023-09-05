namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Google.Protobuf;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Generic;
    using System.Net;

    internal interface ITaskDescriptorExecutor<T> where T : IMessage
    {
        IAsyncEnumerable<ExecuteTaskResponse> ExecuteAsync(
            IPAddress peerAddress,
            T descriptor,
            CancellationToken cancellationToken);
    }

}
