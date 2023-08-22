using System.Net;

namespace Redpoint.AutoDiscovery
{
    public record class NetworkService
    {
        public required string Name { get; set; }
        public required IPEndPoint EndPoint { get; set; }
    }
}