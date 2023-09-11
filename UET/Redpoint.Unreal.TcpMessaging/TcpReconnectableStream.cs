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
        private readonly Concurrency.Semaphore _reconnectionLock;
        private readonly Func<Task<TcpClient>> _reconnectionFactory;
        private readonly Func<TcpClient, Task> _initialNegotiation;
        private bool _canAutoReconnect;
        private bool _isBroken;

        public static async Task<TcpReconnectableStream> CreateAsync(
            ILogger? logger,
            Func<Task<TcpClient>> connectionFactory,
            Func<TcpClient, Task> initialNegotiation)
        {
            var client = await connectionFactory().ConfigureAwait(false);
            await initialNegotiation(client).ConfigureAwait(false);
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
            _reconnectionLock = new Concurrency.Semaphore(1);
            _reconnectionFactory = reconnectionFactory;
            _initialNegotiation = initialNegotiation;
            _canAutoReconnect = true;
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

        public async Task ReconnectAsync()
        {
            await ReconnectInternalAsync(_client).ConfigureAwait(false);
        }

        private async Task ReconnectInternalAsync(TcpClient brokenClient)
        {
            await _reconnectionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_client != brokenClient)
                {
                    // Already updated by another task.
                    return;
                }

                _logger?.LogTrace("Disconnected from remote endpoint, reconnecting...");

                _client = await _reconnectionFactory().ConfigureAwait(false);
                if (!_client.Connected)
                {
                    throw new InvalidOperationException();
                }
                await _initialNegotiation(_client).ConfigureAwait(false);
                _isBroken = false;
                _canAutoReconnect = true;
            }
            finally
            {
                _reconnectionLock.Release();
            }
        }

        private static bool IsExceptionDisconnection(Exception ex)
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

        private async Task DoReconnectableOperationAsync(Func<TcpClient, Task> operation)
        {
            if (_isBroken)
            {
                throw new TcpReconnectionRequiredException();
            }

            while (true)
            {
                var client = _client;
                try
                {
                    await operation(client).ConfigureAwait(false);
                    _canAutoReconnect = false;
                    break;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    if (_canAutoReconnect)
                    {
                        await ReconnectInternalAsync(client).ConfigureAwait(false);
                        continue;
                    }
                    else
                    {
                        _isBroken = true;
                        throw new TcpReconnectionRequiredException();
                    }
                }
            }
        }

        private async Task<T> DoReconnectableOperationAsync<T>(Func<TcpClient, Task<T>> operation)
        {
            if (_isBroken)
            {
                throw new TcpReconnectionRequiredException();
            }

            while (true)
            {
                var client = _client;
                try
                {
                    var result = await operation(client).ConfigureAwait(false);
                    _canAutoReconnect = false;
                    return result;
                }
                catch (Exception ex) when (IsExceptionDisconnection(ex))
                {
                    if (_canAutoReconnect)
                    {
                        await ReconnectInternalAsync(client).ConfigureAwait(false);
                        continue;
                    }
                    else
                    {
                        _isBroken = true;
                        throw new TcpReconnectionRequiredException();
                    }
                }
            }
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return DoReconnectableOperationAsync(client => client.GetStream().CopyToAsync(destination, bufferSize, cancellationToken));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return DoReconnectableOperationAsync(client =>
            {
                _logger?.LogTrace($"Reading {buffer.Length} bytes from stream.");
                return client.GetStream().ReadAsync(buffer, offset, count, cancellationToken);
            });
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await DoReconnectableOperationAsync(async client =>
            {
                _logger?.LogTrace($"Reading {buffer.Length} bytes from stream.");
                return await client.GetStream().ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return DoReconnectableOperationAsync(client =>
            {
                return client.GetStream().WriteAsync(buffer, offset, count, cancellationToken);
            });
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await DoReconnectableOperationAsync(async client =>
            {
                await client.GetStream().WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return DoReconnectableOperationAsync(client =>
            {
                return client.GetStream().FlushAsync(cancellationToken);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            var client = _client;
            try
            {
                await client.GetStream().DisposeAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("The operation is not allowed on non-connected sockets", StringComparison.Ordinal))
            {
                // This is fine.
            }
            await base.DisposeAsync().ConfigureAwait(false);
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
