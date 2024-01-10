namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using Google.Protobuf;

    internal static class TcpGrpcMetadataConverter
    {
        public static TcpGrpcMetadata Convert(Metadata? metadata)
        {
            var result = new TcpGrpcMetadata();
            if (metadata != null)
            {
                foreach (var entry in metadata)
                {
                    if (entry.IsBinary)
                    {
                        result.Values.Add(entry.Key, new TcpGrpcMetadataValue { Bytes = ByteString.CopyFrom(entry.ValueBytes) });
                    }
                    else
                    {
                        result.Values.Add(entry.Key, new TcpGrpcMetadataValue { String = entry.Value });
                    }
                }
            }
            return result;
        }

        public static Metadata Convert(TcpGrpcMetadata metadata)
        {
            var result = new Metadata();
            foreach (var kv in metadata.Values)
            {
                switch (kv.Value.ValueCase)
                {
                    case TcpGrpcMetadataValue.ValueOneofCase.Bytes:
                        result.Add(kv.Key, kv.Value.Bytes.ToByteArray());
                        break;
                    case TcpGrpcMetadataValue.ValueOneofCase.String:
                        result.Add(kv.Key, kv.Value.String);
                        break;
                }
            }
            return result;
        }
    }
}
