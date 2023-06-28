namespace Redpoint.Unreal.TcpMessaging
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class TcpReconnectableStream : Stream
    {
        private readonly ILogger? _logger;
        private TcpClient _client;
        private readonly SemaphoreSlim _reconnectionLock;
        private readonly Func<Task<TcpClient>> _reconnectionFactory;
        private readonly Func<TcpClient, Task> _initialNegotiation;

        public static async Task<TcpReconnectableStream> CreateAsync(
            ILogger? logger,
            Func<Task<TcpClient>> connectionFactory,
            Func<TcpClient, Task> initialNegotiation)
        {
            var client = await connectionFactory();
            await initialNegotiation(client);
            return new TcpReconnectableStream(logger, client, connectionFactory, initialNegotiation);
        }

        private TcpReconnectableStream(
            ILogger? logger,
            TcpClient initialClient,
            Func<Task<TcpClient>> reconnectionFactory,
            Func<TcpClient, Task> initialNegotiation)
        {
            _logger = logger;
            _client = initialClient;
            _reconnectionLock = new SemaphoreSlim(1);
            _reconnectionFactory = reconnectionFactory;
            _initialNegotiation = initialNegotiation;
            if (!_client.Connected)
            {
                throw new InvalidOperationException();
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public async Task ForceReconnectAsync()
        {
            await ReconnectAsync(_client);
        }

        private async Task ReconnectAsync(TcpClient brokenClient)
        {
            await _reconnectionLock.WaitAsync();
            try
            {
                if (_client != brokenClient)
                {
                    // Already updated by another task.
                    return;
                }

                _logger?.LogTrace("Disconnected from remote endpoint, reconnecting...");

                _client = await _reconnectionFactory();
                if (!_client.Connected)
                {
                    throw new InvalidOperationException();
                }
                await _initialNegotiation(_client);
            }
            finally
            {
                _reconnectionLock.Release();
            }
        }

        private bool IsExceptionDisconnection(Exception ex)
        {
            switch (ex)
            {
                case SocketException s:
                    return s.SocketErrorCode == SocketError.ConnectionReset;
                case IOException i:
                    switch (i.InnerException)
                    {
                        case SocketException s:
                            return s.SocketErrorCode == SocketError.ConnectionReset;
                        default:
                            return false;
                    }
            }
            return false;
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    await client.GetStream().CopyToAsync(destination, bufferSize, cancellationToken);
                    break;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    _logger?.LogTrace($"Reading {buffer.Length} bytes from stream.");
                    return await client.GetStream().ReadAsync(buffer, offset, count, cancellationToken);
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    _logger?.LogTrace($"Reading {buffer.Length} bytes from stream.");
                    return await client.GetStream().ReadAsync(buffer, cancellationToken);
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    await client.GetStream().WriteAsync(buffer, offset, count, cancellationToken);
                    break;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    await client.GetStream().WriteAsync(buffer, cancellationToken);
                    break;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var client = _client;
                try
                {
                    await client.GetStream().FlushAsync(cancellationToken);
                    break;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    await ReconnectAsync(client);
                    continue;
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            var client = _client;
            try
            {
                await client.GetStream().DisposeAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("The operation is not allowed on non-connected sockets"))
            {
                // This is fine.
            }
        }

        #region Unsupported Synchronous Methods

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
