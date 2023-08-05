namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;

    internal class DefaultToolManager : IToolManager
    {
        public Task<QueryToolResponse> QueryToolAsync(
            QueryToolRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HasToolBlobsResponse> HasToolBlobsAsync(
            HasToolBlobsRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<WriteToolBlobResponse> WriteToolBlobAsync(
            WriteToolBlobRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ConstructToolResponse> ConstructToolAsync(
            ConstructToolRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
