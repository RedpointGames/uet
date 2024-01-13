namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    internal enum TcpGrpcCallType
    {
        Unary,
        ClientStreaming,
        ServerStreaming,
        DuplexStreaming,
    }
}
