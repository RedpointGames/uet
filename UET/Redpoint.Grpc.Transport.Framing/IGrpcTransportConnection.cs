namespace Redpoint.Grpc.Transport.Framing
{
    public interface IGrpcTransportConnection
    {
        ValueTask WriteAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken);

        ValueTask ReadAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken);
    }
}
