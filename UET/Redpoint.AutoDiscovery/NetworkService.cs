using System.Net;

namespace Redpoint.AutoDiscovery
{
    /// <summary>
    /// Represents a discovered network service.
    /// </summary>
    public record class NetworkService
    {
        /// <summary>
        /// The name of the service.
        /// </summary>
        public required string ServiceName { get; set; }

        /// <summary>
        /// The hostname of the target machine the service is running on.
        /// </summary>
        public required string TargetHostname { get; set; }

        /// <summary>
        /// The known address list for the service.
        /// </summary>
        public required IReadOnlyCollection<IPAddress> TargetAddressList { get; set; }

        /// <summary>
        /// The port that the service is running on.
        /// </summary>
        public required int TargetPort { get; set; }
    }
}