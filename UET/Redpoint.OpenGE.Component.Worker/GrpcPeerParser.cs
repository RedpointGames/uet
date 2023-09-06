namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using System;
    using System.Net;

    internal static class GrpcPeerParser
    {
        public static IPEndPoint ParsePeer(ServerCallContext context)
        {
            if (context.Peer.StartsWith("ipv4:"))
            {
                var portIndex = context.Peer.LastIndexOf(':');
                var port = int.Parse(context.Peer.Substring(portIndex + 1));
                var address = context.Peer.Substring(0, portIndex).Substring("ipv4:".Length);
                return new IPEndPoint(IPAddress.Parse(address), port);
            }
            else if (context.Peer.StartsWith("ipv6:"))
            {
                var portIndex = context.Peer.LastIndexOf(':');
                var port = int.Parse(context.Peer.Substring(portIndex + 1));
                var address = context.Peer.Substring(0, portIndex - 1).Substring("ipv6:".Length + 1);
                return new IPEndPoint(IPAddress.Parse(address), port);
            }
            else
            {
                throw new NotSupportedException($"gRPC peer '{context.Peer}' is not in a known format.");
            }
        }
    }
}
