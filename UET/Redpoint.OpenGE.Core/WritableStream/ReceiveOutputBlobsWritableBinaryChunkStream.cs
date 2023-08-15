namespace Redpoint.OpenGE.Core.WritableStream
{
    using Google.Protobuf;
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of <see cref="GrpcWritableBinaryChunkStream{ExecutionResponse}"/>
    /// that is used by the worker to send output blobs to the dispatcher.
    /// </summary>
    public class ReceiveOutputBlobsWritableBinaryChunkStream : GrpcWritableBinaryChunkStream<ExecutionResponse>
    {
        public ReceiveOutputBlobsWritableBinaryChunkStream(
            IAsyncStreamWriter<ExecutionResponse> sendingStream)
            : base(sendingStream)
        {
        }

        protected override ExecutionResponse ConstructForSending(
            ReadOnlyMemory<byte> data,
            long position,
            bool isFinished)
        {
            var response = new ReceiveOutputBlobsResponse
            {
                Data = ByteString.CopyFrom(data.Span),
                FinishWrite = isFinished,
            };
            if (position != 0)
            {
                response.Offset = position;
            }
            else
            {
                response.Format = CompressedBlobsFormat.BrotliSequentialVersion1;
            }
            return new ExecutionResponse
            {
                ReceiveOutputBlobs = response,
            };
        }
    }
}
