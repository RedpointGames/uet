using System;
using System.Collections.Generic;
using System.Net;

namespace GitHub.JPMikkers.Dhcp;

public interface IDhcpServer : IDisposable
{
    event EventHandler<DhcpStopEventArgs?> OnStatusChange;

    IPEndPoint EndPoint { get; set; }
    IPAddress? NetworkPrefix { get; set; }
    IPAddress? ServerAddress { get; set; }
    IPAddress SubnetMask { get; set; }
    IPAddress PoolStart { get; set; }
    IPAddress PoolEnd { get; set; }

    TimeSpan OfferExpirationTime { get; set; }
    TimeSpan LeaseTime { get; set; }
    IList<DhcpClient> Clients { get; }
    string HostName { get; }
    bool Active { get; }
    List<OptionItem> Options { get; set; }
    List<IDhcpMessageInterceptor> Interceptors { get; set; }
    List<ReservationItem> Reservations { get; set; }

    void Start();
    void Stop();
}