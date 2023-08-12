namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IToolManager
    {
        Task<string> GetToolPathAsync(
            long toolXxHash64,
            string toolExecutableName,
            CancellationToken cancellationToken);

        Task<QueryToolResponse> QueryToolAsync(
            QueryToolRequest request,
            CancellationToken cancellationToken);

        Task<HasToolBlobsResponse> HasToolBlobsAsync(
            HasToolBlobsRequest request,
            CancellationToken cancellationToken);

        Task<WriteToolBlobResponse> WriteToolBlobAsync(
            WriteToolBlobRequest initialRequest,
            IWorkerRequestStream requestStream,
            CancellationToken cancellationToken);

        Task<ConstructToolResponse> ConstructToolAsync(
            ConstructToolRequest request,
            CancellationToken cancellationToken);
    }
}
