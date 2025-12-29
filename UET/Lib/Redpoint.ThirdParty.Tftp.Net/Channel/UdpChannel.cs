using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Redpoint.Concurrency;

namespace Tftp.Net.Channel
{
    class UdpChannel : ITransferChannel
    {
        private AsyncEvent<TftpCommandEventArgs> _onCommandReceived = new();
        private AsyncEvent<TftpTransferError> _onError = new();

        public IAsyncEvent<TftpCommandEventArgs> OnCommandReceived => _onCommandReceived;
        public IAsyncEvent<TftpTransferError> OnError => _onError;

        private IPEndPoint endpoint;
        private UdpClient client;
        private readonly CommandSerializer serializer = new CommandSerializer();
        private readonly CommandParser parser = new CommandParser();

        private CancellationTokenSource _cancelReceiving = new();
        private Task _receivingTask;

        public UdpChannel(UdpClient client)
        {
            this.client = client;
            this.endpoint = null;
        }

        public void Open()
        {
            if (client == null)
                throw new ObjectDisposedException("UdpChannel");

            _receivingTask = Task.Run(
                async () =>
                {
                    while (!_cancelReceiving.IsCancellationRequested)
                    {
                        UdpReceiveResult result = await client.ReceiveAsync(_cancelReceiving.Token);

                        IPEndPoint endpoint = new IPEndPoint(0, 0);
                        ITftpCommand command = null;

                        try
                        {
                            byte[] data = result.Buffer;
                            endpoint = result.RemoteEndPoint;

                            command = parser.Parse(data);
                        }
                        catch (SocketException e)
                        {
                            //Handle receive error
                            await RaiseOnError(new NetworkError(e));
                        }
                        catch (TftpParserException e2)
                        {
                            //Handle parser error
                            await RaiseOnError(new NetworkError(e2));
                        }

                        if (command != null)
                        {
                            await RaiseOnCommand(command, endpoint);
                        }
                    }
                },
                CancellationToken.None);
        }

        private async Task RaiseOnCommand(ITftpCommand command, IPEndPoint endpoint)
        {
            await _onCommandReceived.BroadcastAsync(
                new TftpCommandEventArgs
                {
                    Command = command,
                    Endpoint = endpoint,
                },
                CancellationToken.None);
        }

        private async Task RaiseOnError(TftpTransferError error)
        {
            await _onError.BroadcastAsync(
                error,
                CancellationToken.None);
        }

        public void Send(ITftpCommand command)
        {
            if (client == null)
                throw new ObjectDisposedException("UdpChannel");

            if (endpoint == null)
                throw new InvalidOperationException("RemoteEndpoint needs to be set before you can send TFTP commands.");

            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(command, stream);
                byte[] data = stream.GetBuffer();
                client.Send(data, (int)stream.Length, endpoint);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_receivingTask != null)
            {
                _cancelReceiving.Cancel();

                try
                {
                    await _receivingTask;
                }
                catch
                {
                }

                _cancelReceiving.Dispose();

                _receivingTask = null;
                _cancelReceiving = null;
            }

            if (this.client != null)
            {
                client.Close();
                this.client = null;
            }
        }

        public EndPoint RemoteEndpoint
        {
            get
            {
                return endpoint;
            }

            set
            {
                if (!(value is IPEndPoint))
                    throw new NotSupportedException("UdpChannel can only connect to IPEndPoints.");

                if (client == null)
                    throw new ObjectDisposedException("UdpChannel");

                this.endpoint = (IPEndPoint)value;
            }
        }
    }
}
