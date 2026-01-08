using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Tftp.Net.Channel;
using Tftp.Net.Transfer;
using Redpoint.Concurrency;

namespace Tftp.Net
{
    /// <summary>
    /// A simple TFTP server class. <code>Dispose()</code> the server to close the socket that it listens on.
    /// </summary>
    public class TftpServer : IAsyncDisposable
    {
        private AsyncEvent<TftpServerEventHandlerArgs> _onReadRequest = new();
        private AsyncEvent<TftpServerEventHandlerArgs> _onWriteRequest = new();
        private AsyncEvent<TftpTransferError> _onError = new();

        public const int DEFAULT_SERVER_PORT = 69;

        /// <summary>
        /// Fired when the server receives a new read request.
        /// </summary>
        public IAsyncEvent<TftpServerEventHandlerArgs> OnReadRequest => _onReadRequest;

        /// <summary>
        /// Fired when the server receives a new write request.
        /// </summary>
        public IAsyncEvent<TftpServerEventHandlerArgs> OnWriteRequest => _onWriteRequest;

        /// <summary>
        /// Fired when the server encounters an error (for example, a non-parseable request)
        /// </summary>
        public IAsyncEvent<TftpTransferError> OnError => _onError;

        /// <summary>
        /// Server port that we're listening on.
        /// </summary>
        private readonly ITransferChannel serverSocket;

        public TftpServer(IPEndPoint localAddress)
        {
            if (localAddress == null)
                throw new ArgumentNullException("localAddress");

            serverSocket = TransferChannelFactory.CreateServer(localAddress);
            serverSocket.OnCommandReceived.Add(serverSocket_OnCommandReceived);
            serverSocket.OnError.Add(serverSocket_OnError);
        }

        public TftpServer(IPAddress localAddress)
            : this(localAddress, DEFAULT_SERVER_PORT)
        {
        }

        public TftpServer(IPAddress localAddress, int port)
            : this(new IPEndPoint(localAddress, port))
        {
        }

        public TftpServer(int port)
            : this(new IPEndPoint(IPAddress.Any, port))
        {
        }

        public TftpServer()
            : this(DEFAULT_SERVER_PORT)
        {
        }


        /// <summary>
        /// Start accepting incoming connections.
        /// </summary>
        public void Start()
        {
            serverSocket.Open();
        }

        async Task serverSocket_OnError(TftpTransferError error, CancellationToken cancellationToken)
        {
            await RaiseOnError(error, cancellationToken);
        }

        private async Task serverSocket_OnCommandReceived(TftpCommandEventArgs args, CancellationToken cancellationToken)
        {
            //Ignore all other commands
            if (!(args.Command is ReadOrWriteRequest))
                return;

            //Open a connection to the client
            ITransferChannel channel = TransferChannelFactory.CreateConnection(args.Endpoint);

            //Create a wrapper for the transfer request
            ReadOrWriteRequest request = (ReadOrWriteRequest)args.Command;
            ITftpTransfer transfer = request is ReadRequest
                ? (ITftpTransfer)(await LocalReadTransfer.CreateLocalReadTransferAsync(channel, request.Filename, request.Options))
                : (await LocalWriteTransfer.CreateLocalWriteTransferAsync(channel, request.Filename, request.Options));

            if (args.Command is ReadRequest)
                await RaiseOnReadRequest(transfer, args.Endpoint, cancellationToken);
            else if (args.Command is WriteRequest)
                await RaiseOnWriteRequest(transfer, args.Endpoint, cancellationToken);
            else
                throw new Exception("Unexpected tftp transfer request: " + args.Command);
        }

        public async ValueTask DisposeAsync()
        {
            await serverSocket.DisposeAsync();
        }

        private async Task RaiseOnError(TftpTransferError error, CancellationToken cancellationToken)
        {
            await _onError.BroadcastAsync(error, cancellationToken);
        }

        private async Task RaiseOnWriteRequest(ITftpTransfer transfer, EndPoint client, CancellationToken cancellationToken)
        {
            if (_onWriteRequest.HasAnyBindings)
            {
                await _onWriteRequest.BroadcastAsync(
                    new TftpServerEventHandlerArgs
                    {
                        Transfer = transfer,
                        EndPoint = client,
                    },
                    cancellationToken);
            }
            else
            {
                transfer.Cancel(new TftpErrorPacket(0, "Server did not provide a OnWriteRequest handler."));
            }
        }

        private async Task RaiseOnReadRequest(ITftpTransfer transfer, EndPoint client, CancellationToken cancellationToken)
        {
            if (_onReadRequest.HasAnyBindings)
            {
                await _onReadRequest.BroadcastAsync(
                    new TftpServerEventHandlerArgs
                    {
                        Transfer = transfer,
                        EndPoint = client,
                    },
                    cancellationToken);
            }
            else
            {
                transfer.Cancel(new TftpErrorPacket(0, "Server did not provide a OnReadRequest handler."));
            }
        }
    }
}

