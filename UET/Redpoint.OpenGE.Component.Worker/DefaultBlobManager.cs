namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Core.ReadableStream;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.IO.Hashing;
    using System.Threading.Tasks;

    internal class DefaultBlobManager : IBlobManager, IAsyncDisposable
    {
        private readonly IReservationManagerForOpenGE _reservationManagerForOpenGE;
        private readonly ConcurrentDictionary<string, ServerCallContext> _remoteHostLocks;
        private readonly SemaphoreSlim _blobsReservationSemaphore;
        private IReservation? _blobsReservation;
        private bool _disposed;

        public DefaultBlobManager(
            IReservationManagerForOpenGE reservationManagerForOpenGE)
        {
            _reservationManagerForOpenGE = reservationManagerForOpenGE;
            _remoteHostLocks = new ConcurrentDictionary<string, ServerCallContext>();
            _blobsReservationSemaphore = new SemaphoreSlim(1);
            _blobsReservation = null;
            _disposed = false;
        }

        private string ParsePeer(string peer)
        {
            return peer.Substring(0, peer.LastIndexOf(':'));
        }

        public void NotifyServerCallEnded(ServerCallContext context)
        {
            var peerHost = ParsePeer(context.Peer);
            if (_remoteHostLocks.TryGetValue(peerHost, out var compareContext))
            {
                if (context == compareContext)
                {
                    _remoteHostLocks.TryRemove(peerHost, out _);
                }
            }
        }

        public string ConvertAbsolutePathToBuildDirectoryPath(string targetDirectory, string absolutePath)
        {
            var remotifiedPath = absolutePath.RemotifyPath(false);
            if (remotifiedPath == null)
            {
                throw new InvalidOperationException($"Expected path '{absolutePath}' to be rooted and not a UNC path.");
            }

            return Path.Combine(targetDirectory, remotifiedPath.TrimStart(Path.DirectorySeparatorChar));
        }

        public async Task LayoutBuildDirectoryAsync(
            string targetDirectory,
            InputFilesByBlobXxHash64 inputFiles,
            string virtualRootPath,
            CancellationToken cancellationToken)
        {
            var blobsPath = await GetBlobsPath();

            var virtualised = new HashSet<string>(inputFiles.AbsolutePathsToVirtualContent);
            await Parallel.ForEachAsync(
                inputFiles.AbsolutePathsToBlobs.ToAsyncEnumerable(),
                cancellationToken,
                async (kv, cancellationToken) =>
                {
                    var targetPath = ConvertAbsolutePathToBuildDirectoryPath(
                        targetDirectory,
                        kv.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    if (virtualised.Contains(kv.Key))
                    {
                        // Grab the blob content, replace {__OPENGE_VIRTUAL_ROOT__} and then emit it.
                        using var sourceStream = new FileStream(
                            Path.Combine(blobsPath, kv.Value.HexString()),
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read);
                        using var reader = new StreamReader(sourceStream, leaveOpen: true);
                        var content = await reader.ReadToEndAsync(cancellationToken);
                        content = content.Replace("{__OPENGE_VIRTUAL_ROOT__}", virtualRootPath);
                        using var targetStream = new FileStream(
                            targetPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);
                        using var writer = new StreamWriter(targetStream, leaveOpen: true);
                        await writer.WriteAsync(content);
                    }
                    else
                    {
                        File.Copy(
                            Path.Combine(blobsPath, kv.Value.HexString()),
                            targetPath,
                            true);
                    }
                });
        }

        public async Task QueryMissingBlobsAsync(
            ServerCallContext context,
            QueryMissingBlobsRequest request, 
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken)
        {
            // @note: We lock around the peer host, because we don't want
            // multiple tasks on the same host trying to send the same blobs.
            // We release this lock either:
            // - When QueryMissingBlobsAsync detects that there are no blobs
            //   for the dispatcher to send us.
            // - When SendCompressedBlobsAsync receives the final write for
            //   the compressed blobs.
            // - When the client disconnects for any reason, which is tracked
            //   on the ServerCallContext object and notified via
            //   NotifyServerCallEndedAsync.
            var peerHost = ParsePeer(context.Peer);
            while (!_remoteHostLocks.TryAdd(peerHost, context))
            {
                await Task.Delay(200 * Random.Shared.Next(1, 5), cancellationToken);
            }
            var retainLock = false;
            try
            {
                var blobsPath = await GetBlobsPath();

                var requested = new HashSet<long>(request.BlobXxHash64);
                var exists = new HashSet<long>();
                foreach (var hash in request.BlobXxHash64)
                {
                    var targetPath = Path.Combine(blobsPath, hash.HexString());
                    if (File.Exists(targetPath))
                    {
                        exists.Add(hash);
                    }
                }
                var notExists = new HashSet<long>(requested);
                notExists.ExceptWith(exists);

                var response = new QueryMissingBlobsResponse();
                response.MissingBlobXxHash64.AddRange(notExists);
                await responseStream.WriteAsync(new ExecutionResponse
                {
                    QueryMissingBlobs = response,
                });

                // @note: If we have no missing blobs, then the client will never call
                // SendCompressedBlobsAsync and thus we should not retain the lock or
                // it won't be released until the RPC is closed.
                retainLock = response.MissingBlobXxHash64.Count > 0;
            }
            finally
            {
                if (!retainLock)
                {
                    _remoteHostLocks.TryRemove(peerHost, out _);
                }
            }
        }

        public async Task SendCompressedBlobsAsync(
            ServerCallContext context,
            ExecutionRequest initialRequest,
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken)
        {
            var peerHost = ParsePeer(context.Peer);
            if (!_remoteHostLocks.TryGetValue(peerHost, out var currentContext) ||
                currentContext != context)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "You must not send SendCompressedBlobsRequest until QueryMissingBlobsResponse has arrived."));
            }

            var blobsPath = await GetBlobsPath();

            // At this point, we're in the lock for this peer, so we can safely
            // receive the data and stream it out.
            IDisposable? lockFile = null;
            FileStream? currentStream = null;
            long currentBytesRemaining = 0;
            XxHash64? hash = null;
            try
            {
                using (var destination = new SequentialVersion1Decoder(
                    (blobHash, blobLength) =>
                    {
                        if (lockFile != null)
                        {
                            throw new InvalidOperationException();
                        }
                        lockFile = LockFile.TryObtainLock(
                            Path.Combine(blobsPath, blobHash.HexString() + ".lock"));
                        while (lockFile == null)
                        {
                            // @hack: This is super questionable, but we know that
                            // the blob writing (and consequently the lock file
                            // duration) in other request threads is *not* asynchronous,
                            // The lock will never be held over an async yield even
                            // across RPCs, so sleeping the current thread while another
                            // RPC's thread completes writing to the file is probably
                            // safe.
                            Thread.Sleep(200);
                        }
                        hash = new XxHash64();
                        currentStream = new FileStream(
                            Path.Combine(blobsPath, blobHash.HexString() + ".tmp"),
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);
                        currentStream.SetLength(blobLength);
                        currentBytesRemaining = blobLength;
                    },
                    (blobHash, blobOffset, buffer, bufferOffset, bufferCount) =>
                    {
                        // @note: We don't seek the file streams because we know we're writing sequentially.
                        currentStream!.Write(buffer, bufferOffset, bufferCount);
                        hash!.Append(new Span<byte>(buffer, bufferOffset, bufferCount));
                        currentBytesRemaining -= bufferCount;
                        if (currentBytesRemaining < 0)
                        {
                            throw new InvalidOperationException();
                        }
                        else if (currentBytesRemaining == 0)
                        {
                            currentStream.Flush();
                            currentStream.Dispose();
                            currentStream = null;
                            var invalid = false;
                            if (BitConverter.ToInt64(hash.GetCurrentHash()) == blobHash)
                            {
                                File.Move(
                                    Path.Combine(blobsPath, blobHash.HexString() + ".tmp"),
                                    Path.Combine(blobsPath, blobHash.HexString()));
                            }
                            else
                            {
                                try
                                {
                                    File.Delete(
                                        Path.Combine(blobsPath, blobHash.HexString() + ".tmp"));
                                }
                                catch
                                {
                                }
                                invalid = true;
                            }
                            if (lockFile != null)
                            {
                                lockFile.Dispose();
                                lockFile = null;
                            }
                            if (invalid)
                            {
                                throw new RpcException(new Status(
                                    StatusCode.InvalidArgument,
                                    "The received blob did not hash to the hash it was associated with."));
                            }
                        }
                    }))
                {
                    using (var source = new SendCompressedBlobsReadableBinaryChunkStream(
                        initialRequest,
                        requestStream))
                    {
                        using (var decompressor = new BrotliStream(source, CompressionMode.Decompress))
                        {
                            await decompressor.CopyToAsync(destination);
                        }
                    }
                }

                await responseStream.WriteAsync(new ExecutionResponse
                {
                    SendCompressedBlobs = new SendCompressedBlobsResponse(),
                }, cancellationToken);
            }
            finally
            {
                if (currentStream != null)
                {
                    currentStream.Flush();
                    currentStream.Dispose();
                    currentStream = null;
                }
                if (lockFile != null)
                {
                    lockFile.Dispose();
                    lockFile = null;
                }
                if (hash != null)
                {
                    hash = null;
                }

                // Release the lock to allow other requests from the same host
                // to come through.
                _remoteHostLocks.TryRemove(peerHost, out _);
            }
        }

        private class PeerLockDisposable : IDisposable
        {
            private ConcurrentDictionary<string, bool> _remoteHostLocks;
            private string _peerHost;

            public PeerLockDisposable(
                ConcurrentDictionary<string, bool> remoteHostLocks, 
                string peerHost)
            {
                _remoteHostLocks = remoteHostLocks;
                _peerHost = peerHost;
            }

            public void Dispose()
            {
                _remoteHostLocks.TryRemove(_peerHost, out _);
            }
        }

        private async Task<string> GetBlobsPath()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultToolManager));
            }
            if (_blobsReservation != null)
            {
                return _blobsReservation.ReservedPath;
            }
            await _blobsReservationSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DefaultToolManager));
                }
                if (_blobsReservation != null)
                {
                    return _blobsReservation.ReservedPath;
                }
                _blobsReservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync("Blobs");
                return _blobsReservation.ReservedPath;
            }
            finally
            {
                _blobsReservationSemaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _blobsReservationSemaphore.WaitAsync();
            try
            {
                if (_blobsReservation != null)
                {
                    await _blobsReservation.DisposeAsync();
                }
                _disposed = true;
            }
            finally
            {
                _blobsReservationSemaphore.Release();
            }
        }
    }
}
