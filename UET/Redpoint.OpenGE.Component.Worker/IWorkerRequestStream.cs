namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IWorkerRequestStream
    {
        Task<bool> MoveNext(CancellationToken cancellationToken);

        ExecutionRequest Current { get; }
    }
}
