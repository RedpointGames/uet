namespace Redpoint.OpenGE.Core.ReadableStream
{
    using Google.Protobuf;
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;

    /// <summary>
    /// An implementation of <see cref="GrpcReadableBinaryChunkStream{ExecutionRequest, ExecutionResponse}"/>
    /// that is by the dispatcher to receive the output blob data from the worker.
    /// </summary>
    public class ReceiveOutputBlobsReadableBinaryChunkStream : GrpcReadableBinaryChunkStream<ExecutionRequest, ExecutionResponse>
    {
        public ReceiveOutputBlobsReadableBinaryChunkStream(
            BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> stream)
            : base(stream)
        {
        }

        public ReceiveOutputBlobsReadableBinaryChunkStream(
            ExecutionResponse initial,
            IAsyncStreamReader<ExecutionResponse> receivingStream)
            : base(initial, receivingStream)
        {
        }

        protected override void ValidateOutbound(ExecutionResponse outbound)
        {
            ArgumentNullException.ThrowIfNull(outbound);
            if (outbound.ResponseCase != ExecutionResponse.ResponseOneofCase.ReceiveOutputBlobs)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected only ReceiveOutputBlobsResponse at this time."));
            }
        }

        protected override ByteString GetData(ExecutionResponse outbound)
        {
            ArgumentNullException.ThrowIfNull(outbound);
            return outbound.ReceiveOutputBlobs.Data;
        }

        protected override bool GetFinished(ExecutionResponse outbound)
        {
            ArgumentNullException.ThrowIfNull(outbound);
            return outbound.ReceiveOutputBlobs.FinishWrite;
        }
    }
}
