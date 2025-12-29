using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;

namespace GitHub.JPMikkers.Dhcp;

public class UdpSocketLinux : IUdpSocket
{
    // see https://github.com/dotnet/runtime/issues/83525
    const int SO_BINDTODEVICE = 25;
    const int SOL_SOCKET = 1;

    private bool _disposed;                                     // true => object is disposed
    private readonly bool _IPv6;                                // true => it's an IPv6 connection
    private readonly Socket _socket;                            // The active socket
    private readonly int _maxPacketSize;                        // size of packets we'll try to receive

    private readonly IPEndPoint _localEndPoint;

    public IPEndPoint LocalEndPoint
    {
        get
        {
            return _localEndPoint;
        }
    }

    public UdpSocketLinux(IPEndPoint localEndPoint, int maxPacketSize, bool dontFragment, short ttl)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            Console.WriteLine($"{nic.Id}");

        }

        var selectedNic = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.GetIPProperties().UnicastAddresses.Select(a => a.Address).Contains(localEndPoint.Address))
            .FirstOrDefault();

        if (selectedNic is null)
        {
            throw new UdpSocketException($"Can't find the appropriate network interface associated with endpoint '{localEndPoint}'") { IsFatal = true };
        }

        _maxPacketSize = maxPacketSize;
        _disposed = false;

        _IPv6 = (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6);

        _socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.EnableBroadcast = true;
        _socket.ExclusiveAddressUse = false;    //_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        _socket.SendBufferSize = 65536;
        _socket.ReceiveBufferSize = 65536;
        if (!_IPv6) _socket.DontFragment = dontFragment;
        if (ttl >= 0)
        {
            _socket.Ttl = ttl;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _socket.SetRawSocketOption(SOL_SOCKET, SO_BINDTODEVICE, Encoding.UTF8.GetBytes(selectedNic.Id));
        }
        _socket.Bind(new IPEndPoint(IPAddress.Any, localEndPoint.Port));
        _localEndPoint = localEndPoint;
    }


    public async Task<(IPEndPoint, ReadOnlyMemory<byte>)> Receive(CancellationToken cancellationToken)
    {
        try
        {
            var mem = new Memory<byte>(new byte[_maxPacketSize]);
            var result = await _socket.ReceiveFromAsync(mem, new IPEndPoint(IPAddress.Any, 0), cancellationToken);

            if (result.RemoteEndPoint is IPEndPoint endpoint)
            {
                return (endpoint, mem[..result.ReceivedBytes]);
            }
            else
            {
                throw new InvalidCastException("unexpected endpoint type");
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize)
        {
            // someone tried to send a message bigger than _maxPacketSize
            // discard it, and start receiving the next packet
            throw new UdpSocketException($"{nameof(Receive)} error: {ex.Message}", ex) { IsFatal = false };
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            // ConnectionReset is reported when the remote port wasn't listening.
            // Since we're using UDP messaging we don't care about this -> continue receiving.
            throw new UdpSocketException($"{nameof(Receive)} error: {ex.Message}", ex) { IsFatal = false };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // everything else is fatal
            throw new UdpSocketException($"{nameof(Receive)} error: {ex.Message}", ex) { IsFatal = true };
        }
    }

    /// <summary>
    /// Sends a packet of bytes to the specified EndPoint using an UDP datagram.
    /// </summary>
    /// <param name="endPoint">Target for the data</param>
    /// <param name="msg">Data to send</param>
    /// <param name="cancellationToken"></param>
    public async Task Send(IPEndPoint endPoint, ReadOnlyMemory<byte> msg, CancellationToken cancellationToken)
    {
        try
        {
            await _socket.SendToAsync(msg, endPoint, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UdpSocketException($"{nameof(Send)}", ex) { IsFatal = true };
        }
    }

    ~UdpSocketLinux()
    {
        try
        {
            Dispose(false);
        }
        catch
        {
            // never let any exception escape the finalizer, or else your process will be killed.
        }
    }

    /// <summary>
    /// Implements <see cref="IDisposable.Dispose"/>
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                }
                catch (Exception)
                {
                    // socket tends to complain a lot during close. just eat those exceptions.
                }
            }
        }
    }
}
