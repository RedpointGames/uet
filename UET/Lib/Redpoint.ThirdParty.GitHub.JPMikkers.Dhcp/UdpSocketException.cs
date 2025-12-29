using System;

namespace GitHub.JPMikkers.Dhcp;

public class UdpSocketException : Exception
{
    public required bool IsFatal { get; init; }

    public UdpSocketException()
    {
    }

    public UdpSocketException(string message) : base(message)
    {
    }

    public UdpSocketException(string message, Exception inner) : base(message, inner)
    {
    }
}