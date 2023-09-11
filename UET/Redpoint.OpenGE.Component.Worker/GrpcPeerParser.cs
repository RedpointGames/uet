namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using System;
    using System.Globalization;
    using System.Net;

    internal static class GrpcPeerParser
    {
        public static IPEndPoint ParsePeer(ServerCallContext context)
        {
            if (context.Peer.StartsWith("ipv4:", StringComparison.Ordinal))
            {
                var portIndex = context.Peer.LastIndexOf(':');
                var port = int.Parse(context.Peer[(portIndex + 1)..], CultureInfo.InvariantCulture);
                var address = context.Peer[..portIndex]["ipv4:".Length..];
                return new IPEndPoint(IPAddress.Parse(address), port);
            }
            else if (context.Peer.StartsWith("ipv6:", StringComparison.Ordinal))
            {
                var portIndex = context.Peer.LastIndexOf(':');
                var port = int.Parse(context.Peer[(portIndex + 1)..], CultureInfo.InvariantCulture);
                var address = context.Peer[..(portIndex - 1)][("ipv6:".Length + 1)..];
                return new IPEndPoint(IPAddress.Parse(address), port);
            }
            else
            {
                throw new NotSupportedException($"gRPC peer '{context.Peer}' is not in a known format.");
            }
        }
    }
}
