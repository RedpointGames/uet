namespace Redpoint.OpenGE.Core.ReadableStream
{
    using Google.Protobuf;
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;

    /// <summary>
    /// An implementation of <see cref="GrpcReadableBinaryChunkStream{ExecutionRequest, ExecutionResponse}"/>
    /// that is by the worker to receive input blobs from the dispatcher.
    /// </summary>
    public class SendCompressedBlobsReadableBinaryChunkStream : GrpcReadableBinaryChunkStream<ExecutionResponse, ExecutionRequest>
    {
        public SendCompressedBlobsReadableBinaryChunkStream(
            BufferedAsyncDuplexStreamingCall<ExecutionResponse, ExecutionRequest> stream)
            : base(stream)
        {
        }

        public SendCompressedBlobsReadableBinaryChunkStream(
            ExecutionRequest initial,
            IAsyncStreamReader<ExecutionRequest> receivingStream)
            : base(initial, receivingStream)
        {
        }

        protected override void ValidateOutbound(ExecutionRequest outbound)
        {
            if (outbound.RequestCase != ExecutionRequest.RequestOneofCase.SendCompressedBlobs)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected only SendCompressedBlobsRequest at this time."));
            }
        }

        protected override ByteString GetData(ExecutionRequest outbound)
        {
            return outbound.SendCompressedBlobs.Data;
        }

        protected override bool GetFinished(ExecutionRequest outbound)
        {
            return outbound.SendCompressedBlobs.FinishWrite;
        }
    }
}
