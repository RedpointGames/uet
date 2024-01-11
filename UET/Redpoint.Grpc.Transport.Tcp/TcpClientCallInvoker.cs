namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using Google.Protobuf;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    public class TcpClientCallInvoker : CallInvoker
    {
        private readonly IPEndPoint _endpoint;

        public TcpClientCallInvoker(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
        }

        /*
        private static void WriteVersionHeader(BinaryWriter writer)
        {


            writer.Write((Int16)1);
        }

        private static void WriteRequestHeader(BinaryWriter writer, IMethod method)
        {
            writer.Write(method.ServiceName);
            writer.Write(method.Name);
        }

        private static void WriteMessageWithFrame(BinaryWriter writer, byte[] data)
        {
            if (data.LongLength > int.MaxValue)
            {
                throw new ArgumentException("data is too long", nameof(data));
            }
            writer.Write((Int32)data.Length);
            writer.Write(data);
        }

        private static byte[] ReadMessageWithFrame(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
            {
                throw new InvalidOperationException("invalid length specified by message frame");
            }
            else if (length == 0)
            {
                return Array.Empty<byte>();
            }
            else
            {
                return reader.ReadBytes(length);
            }
        }

        private static ValueTask WriteVersionHeaderAsync(BinaryWriter writer)
        {
            //BitConverter.GetBytes()
            writer.Write((Int16)1);
        }

        private static ValueTask WriteRequestHeaderAsync(BinaryWriter writer, IMethod method)
        {
            writer.Write(method.ServiceName);
            writer.Write(method.Name);
        }

        private static ValueTask WriteMessageWithFrameAsync(BinaryWriter writer, byte[] data)
        {
            if (data.LongLength > int.MaxValue)
            {
                throw new ArgumentException("data is too long", nameof(data));
            }
            writer.Write((Int32)data.Length);
            writer.Write(data);
        }

        private static ValueTask<byte[]> ReadMessageWithFrameAsync(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
            {
                throw new InvalidOperationException("invalid length specified by message frame");
            }
            else if (length == 0)
            {
                return Array.Empty<byte>();
            }
            else
            {
                return reader.ReadBytes(length);
            }
        }*/

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var version = new TcpRequestVersion
            {
                Version = 1,
            };
            var header = new TcpRequestHeaderV1
            {
                ServiceName = method.ServiceName,
                MethodName = method.Name,
            };

            using (var client = new TcpClient())
            {
                client.Connect(_endpoint);
                using (var reader = new BinaryReader(client.GetStream(), Encoding.UTF8, leaveOpen: true))
                {
                    using (var writer = new BinaryWriter(client.GetStream(), Encoding.UTF8, leaveOpen: true))
                    {
                        WriteVersionHeader(writer);
                        WriteRequestHeader(writer, method);
                        WriteMessageWithFrame(writer, method.RequestMarshaller.Serializer(request));
                        return method.ResponseMarshaller.Deserializer(ReadMessageWithFrame(reader));
                    }
                }
            }
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            throw new NotImplementedException();
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            throw new NotImplementedException();
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            throw new NotImplementedException();
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            throw new NotImplementedException();
        }
    }
}