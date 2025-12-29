using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Tftp.Net.Channel;
using Tftp.Net.Transfer.States;
using Tftp.Net.Transfer;

namespace Tftp.Net
{
    /// <summary>
    /// A TFTP client that can connect to a TFTP server.
    /// </summary>
    public class TftpClient
    {
        private const int DEFAULT_SERVER_PORT = 69;
        private readonly IPEndPoint remoteAddress;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="remoteAddress">Address of the server that you would like to connect to.</param>
        public TftpClient(IPEndPoint remoteAddress)
        {
            this.remoteAddress = remoteAddress;
        }

        /// <summary>
        /// Connects to a server
        /// </summary>
        /// <param name="ip">Address of the server that you want connect to.</param>
        /// <param name="port">Port on the server that you want connect to (default: 69)</param>
        public TftpClient(IPAddress ip, int port)
            : this(new IPEndPoint(ip, port))
        {
        }

        /// <summary>
        /// Connects to a server on port 69.
        /// </summary>
        /// <param name="ip">Address of the server that you want connect to.</param>
        public TftpClient(IPAddress ip)
            : this(new IPEndPoint(ip, DEFAULT_SERVER_PORT))
        {
        }

        /// <summary>
        /// Connect to a server by hostname.
        /// </summary>
        /// <param name="host">Hostname or ip to connect to</param>
        public TftpClient(String host)
            : this(host, DEFAULT_SERVER_PORT)
        {
        }

        /// <summary>
        /// Connect to a server by hostname and port .
        /// </summary>
        /// <param name="host">Hostname or ip to connect to</param>
        /// <param name="port">Port to connect to</param>
        public TftpClient(String host, int port)
        {
            IPAddress ip = Dns.GetHostAddresses(host).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

            if (ip == null)
                throw new ArgumentException("Could not convert '" + host + "' to an IPv4 address.", "host");

            this.remoteAddress = new IPEndPoint(ip, port);
        }

        /// <summary>
        /// GET a file from the server.
        /// You have to call Start() on the returned ITftpTransfer to start the transfer.
        /// </summary>
        public async Task<ITftpTransfer> Download(String filename)
        {
            ITransferChannel channel = TransferChannelFactory.CreateConnection(remoteAddress);
            return await RemoteReadTransfer.CreateRemoteReadTransferAsync(channel, filename);
        }

        /// <summary>
        /// PUT a file from the server.
        /// You have to call Start() on the returned ITftpTransfer to start the transfer.
        /// </summary>
        public async Task<ITftpTransfer> Upload(String filename)
        {
            ITransferChannel channel = TransferChannelFactory.CreateConnection(remoteAddress);
            return await RemoteWriteTransfer.CreateRemoteWriteTransferAsync(channel, filename);
        }
    }
}
