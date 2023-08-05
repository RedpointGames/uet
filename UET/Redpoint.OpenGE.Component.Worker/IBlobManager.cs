namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IBlobManager
    {
        Task<QueryMissingBlobsResponse> QueryMissingBlobsAsync(
            QueryMissingBlobsRequest request,
            CancellationToken cancellationToken);

        Task<SendCompressedBlobsResponse> SendCompressedBlobsAsync(
            SendCompressedBlobsRequest request,
            CancellationToken cancellationToken);
    }
}
