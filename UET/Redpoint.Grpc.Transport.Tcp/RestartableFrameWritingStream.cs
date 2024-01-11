namespace Redpoint.Grpc.Transport.Framing
{
    using Google.Protobuf;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    internal class GrpcTransportConnection : IDisposable
    {
        private readonly RestartableFrameReadingStream _restartableFrameStream;
        private readonly NetworkStream _writeStream;

        public GrpcTransportConnection(TcpClient client)
        {
            _restartableFrameStream = new RestartableFrameReadingStream(client.GetStream());
            _writeStream = client.GetStream();
        }

        public void Write<T>(T value) where T : IMessage
        {

        }

        public bool

        public T ReadExpected<T>() where T : IMessage, new()
        {

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
