using System;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpStopEventArgs : EventArgs
{
    public required Exception? Reason { get; init; }
}
