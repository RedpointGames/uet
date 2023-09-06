namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.IO;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Core.ReadableStream;
    using Redpoint.OpenGE.Core.WritableStream;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.IO.Hashing;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultBlobManager : IBlobManager, IAsyncDisposable
    {
        private readonly IReservationManagerForOpenGE _reservationManagerForOpenGE;
        private readonly ConcurrentDictionary<IPEndPoint, ServerCallContext> _remoteHostLocks;
        private readonly SemaphoreSlim _blobsReservationSemaphore;
        private IReservation? _blobsReservation;
        private bool _disposed;

        public DefaultBlobManager(
            IReservationManagerForOpenGE reservationManagerForOpenGE)
        {
            _reservationManagerForOpenGE = reservationManagerForOpenGE;
            _remoteHostLocks = new ConcurrentDictionary<IPEndPoint, ServerCallContext>();
            _blobsReservationSemaphore = new SemaphoreSlim(1);
            _blobsReservation = null;
            _disposed = false;
        }

        public void NotifyServerCallEnded(ServerCallContext context)
        {
            var peerHost = GrpcPeerParser.ParsePeer(context);
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
            string? remotifiedPath;
            if (absolutePath == null || absolutePath.Length < 3)
            {
                throw new InvalidOperationException($"Absolute path for ConvertAbsolutePathToBuildDirectoryPath was an empty string.");
            }
            else if (absolutePath.Length == 3)
            {
                remotifiedPath = absolutePath[0].ToString();
            }
            else
            {
                remotifiedPath = absolutePath[0] + absolutePath.Substring(2);
            }
            if (remotifiedPath == null)
            {
                throw new InvalidOperationException($"Expected path '{absolutePath}' to be rooted and not a UNC path.");
            }

            return Path.Combine(targetDirectory, remotifiedPath.TrimStart(Path.DirectorySeparatorChar));
        }

        public async Task LayoutBuildDirectoryAsync(
            string targetDirectory,
            InputFilesByBlobXxHash64 inputFiles,
            CancellationToken cancellationToken)
        {
            var blobsPath = await GetBlobsPath();

            // Figure out what files were previously created in this workspace,
            // so we can compute the different in files we have and the files
            // we want. This allows us to remove files we don't want.
            var filesToDelete = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var existingFilesPath = Path.Combine(targetDirectory, ".bloblist");
            if (File.Exists(existingFilesPath))
            {
                using (var reader = new StreamReader(existingFilesPath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        filesToDelete.Add(line!);
                    }
                }
            }
            var filesToCreate = new HashSet<string>(inputFiles.AbsolutePathsToBlobs.Keys, StringComparer.InvariantCultureIgnoreCase);
            filesToDelete.ExceptWith(filesToCreate);

            // Remove files we don't want.
            Parallel.ForEach(
                filesToDelete,
                path =>
                {
                    var targetPath = ConvertAbsolutePathToBuildDirectoryPath(
                        targetDirectory,
                        path);
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                });

            // Write the new list of files that we will be writing. We do this after the delete
            // operation so that we know the old list won't be needed any more.
            using (var writer = new StreamWriter(existingFilesPath))
            {
                foreach (var path in filesToCreate)
                {
                    writer.WriteLine(path);
                }
            }

            // Write out all of the files we want in this directory.
            Parallel.ForEach(
                inputFiles.AbsolutePathsToBlobs,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken
                },
                kv =>
                {
                    var targetPath = ConvertAbsolutePathToBuildDirectoryPath(
                        targetDirectory,
                        kv.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    if (!File.Exists(Path.Combine(blobsPath, kv.Value.XxHash64.HexString())))
                    {
                        throw new InvalidOperationException($"Expected blob file was not transferred from dispatcher: {kv.Value.XxHash64.HexString()}");
                    }
                    if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
                    {
                        HardLink.CreateHardLink(
                            targetPath,
                            Path.Combine(blobsPath, kv.Value.XxHash64.HexString()),
                            true);
                    }
                    else
                    {
                        File.Copy(
                            Path.Combine(blobsPath, kv.Value.XxHash64.HexString()),
                            targetPath,
                            true);
                    }
                    if (kv.Value.LastModifiedUtcTicks != 0)
                    {
                        File.SetLastWriteTimeUtc(
                            targetPath,
                            new DateTime(kv.Value.LastModifiedUtcTicks, DateTimeKind.Utc));
                    }
                });
        }

        public bool IsTransferringFromPeer(ServerCallContext context)
        {
            var peerHost = GrpcPeerParser.ParsePeer(context);
            return _remoteHostLocks.TryGetValue(peerHost, out _);
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
            var peerHost = GrpcPeerParser.ParsePeer(context);
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
            var peerHost = GrpcPeerParser.ParsePeer(context);
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
                                    Path.Combine(blobsPath, blobHash.HexString()),
                                    true);
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

        public async Task<OutputFilesByBlobXxHash64> CaptureOutputBlobsFromBuildDirectoryAsync(
            string targetDirectory,
            IEnumerable<string> outputAbsolutePaths,
            CancellationToken cancellationToken)
        {
            var blobsPath = await GetBlobsPath();

            var results = new ConcurrentDictionary<string, long>();

            var virtualised = new HashSet<string>(outputAbsolutePaths);
            await Parallel.ForEachAsync(
                outputAbsolutePaths.ToAsyncEnumerable(),
                cancellationToken,
                async (path, cancellationToken) =>
                {
                    var targetPath = ConvertAbsolutePathToBuildDirectoryPath(
                        targetDirectory,
                        path);
                    if (File.Exists(targetPath))
                    {
                        var fileHash = (await XxHash64Helpers.HashFile(targetPath, cancellationToken)).hash;
                        var blobPath = Path.Combine(blobsPath, fileHash.HexString());
                        results[path] = fileHash;
                        if (!File.Exists(blobPath))
                        {
                            var lockFile = LockFile.TryObtainLock(
                                Path.Combine(blobsPath, fileHash.HexString() + ".lock"));
                            while (lockFile == null)
                            {
                                // @hack: This is super questionable, but we know that
                                // the file copy below is *not* asynchronous, nor is blob
                                // writing in other request threads.
                                //
                                // The lock will never be held over an async yield even
                                // across RPCs, so sleeping the current thread while another
                                // RPC's thread completes writing to the file is probably
                                // safe.
                                Thread.Sleep(200);
                            }
                            try
                            {
                                if (!File.Exists(blobPath))
                                {
                                    File.Copy(
                                        targetPath,
                                        blobPath,
                                        true);
                                }
                            }
                            finally
                            {
                                lockFile.Dispose();
                            }
                        }
                    }
                });

            return new OutputFilesByBlobXxHash64
            {
                AbsolutePathsToBlobs = { results }
            };
        }

        public async Task ReceiveOutputBlobsAsync(
            ServerCallContext context,
            ExecutionRequest request,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken)
        {
            var blobsPath = await GetBlobsPath();

            var allEntriesByBlobHash = new ConcurrentDictionary<long, BlobInfo>();
            var requestedBlobHashes = request.ReceiveOutputBlobs.BlobXxHash64;

            await Parallel.ForEachAsync(
                requestedBlobHashes.ToAsyncEnumerable(),
                cancellationToken,
                (blobHash, cancellationToken) =>
                {
                    var blobPath = Path.Combine(blobsPath, blobHash.HexString());
                    var blobFileInfo = new FileInfo(blobPath);
                    if (blobFileInfo.Exists)
                    {
                        allEntriesByBlobHash[blobHash] = new BlobInfo
                        {
                            ByteLength = blobFileInfo.Length,
                            Content = null,
                            Path = blobPath,
                        };
                    }
                    return ValueTask.CompletedTask;
                });

            await using (var destination = new ReceiveOutputBlobsWritableBinaryChunkStream(responseStream))
            {
                await using (var compressor = new BrotliStream(destination, CompressionMode.Compress))
                {
                    using (var source = new SequentialVersion1Encoder(
                        allEntriesByBlobHash,
                        requestedBlobHashes))
                    {
                        await source.CopyToAsync(compressor, cancellationToken);
                    }
                }
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
