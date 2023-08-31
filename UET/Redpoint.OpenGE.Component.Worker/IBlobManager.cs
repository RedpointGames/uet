namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IBlobManager
    {
        void NotifyServerCallEnded(ServerCallContext context);

        string ConvertAbsolutePathToBuildDirectoryPath(
            string targetDirectory,
            string absolutePath);

        Task LayoutBuildDirectoryAsync(
            string targetDirectory,
            InputFilesByBlobXxHash64 inputFiles,
            CancellationToken cancellationToken);

        bool IsTransferringFromPeer(ServerCallContext context);

        Task QueryMissingBlobsAsync(
            ServerCallContext context,
            QueryMissingBlobsRequest request,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken);

        Task SendCompressedBlobsAsync(
            ServerCallContext context,
            ExecutionRequest initialRequest,
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken);

        Task<OutputFilesByBlobXxHash64> CaptureOutputBlobsFromBuildDirectoryAsync(
            string targetDirectory,
            IEnumerable<string> outputAbsolutePaths,
            CancellationToken cancellationToken);

        Task ReceiveOutputBlobsAsync(
            ServerCallContext context,
            ExecutionRequest request,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken);
    }
}
