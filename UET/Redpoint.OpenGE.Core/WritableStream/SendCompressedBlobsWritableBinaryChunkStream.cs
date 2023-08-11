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
    /// An implementation of <see cref="GrpcWritableBinaryChunkStream{ExecutionRequest}"/>
    /// that is used by the dispatcher to send input blobs to the dispatcher.
    /// </summary>
    public class SendCompressedBlobsWritableBinaryChunkStream : GrpcWritableBinaryChunkStream<ExecutionRequest>
    {
        public SendCompressedBlobsWritableBinaryChunkStream(
            IAsyncStreamWriter<ExecutionRequest> sendingStream) 
            : base(sendingStream)
        {
        }

        protected override ExecutionRequest ConstructForSending(
            ReadOnlyMemory<byte> data, 
            long position, 
            bool isFinished)
        {
            var request = new SendCompressedBlobsRequest
            {
                Data = ByteString.CopyFrom(data.Span),
                FinishWrite = isFinished,
            };
            if (position == 0)
            {
                request.Offset = 0;
            }
            else
            {
                request.Format = CompressedBlobsFormat.BrotliSequentialVersion1;
            }
            return new ExecutionRequest
            {
                SendCompressedBlobs = request,
            };
        }
    }
}
