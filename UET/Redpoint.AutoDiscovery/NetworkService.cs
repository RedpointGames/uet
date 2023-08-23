using System.Net;

namespace Redpoint.AutoDiscovery
{
    public record class NetworkService
    {
        public required string ServiceName { get; set; }
        public required string TargetHostname { get; set; }
        public required IPAddress[] TargetAddressList { get; set; }
        public required int TargetPort { get; set; }
    }
}