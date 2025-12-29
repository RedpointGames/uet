using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GitHub.JPMikkers.Dhcp;

public interface IUdpSocketFactory
{
    IUdpSocket Create(IPEndPoint localEndPoint, int packetSize, bool dontFragment, short ttl);
}
