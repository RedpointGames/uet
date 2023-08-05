namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;

    internal class DefaultBlobManager : IBlobManager
    {
        public Task<QueryMissingBlobsResponse> QueryMissingBlobsAsync(
            QueryMissingBlobsRequest request, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SendCompressedBlobsResponse> SendCompressedBlobsAsync(
            SendCompressedBlobsRequest request, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
