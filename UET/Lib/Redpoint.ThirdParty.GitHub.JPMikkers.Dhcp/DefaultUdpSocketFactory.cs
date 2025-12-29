using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GitHub.JPMikkers.Dhcp;

public class DefaultUdpSocketFactory : IUdpSocketFactory
{
    private readonly ILogger _logger;

    public DefaultUdpSocketFactory(ILogger<DefaultUdpSocketFactory>? logger = null)
    {
        // see https://blog.rsuter.com/logging-with-ilogger-recommendations-and-best-practices/
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    public IUdpSocket Create(IPEndPoint localEndPoint, int packetSize, bool dontFragment, short ttl)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogInformation("creating UDPSocket of type {SocketImpl}", nameof(UdpSocketLinux));
            return new UdpSocketLinux(localEndPoint, packetSize, dontFragment, ttl);
        }
        else
        {
            _logger.LogInformation("creating UDPSocket of type {SocketImpl}", nameof(UdpSocketWindows));
            return new UdpSocketWindows(localEndPoint, packetSize, dontFragment, ttl);
        }
    }
}
