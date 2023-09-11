namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System;
    using System.Threading.Tasks;
    using System.IO.Hashing;
    using Grpc.Core;

    internal class DefaultToolManager : IToolManager, IAsyncDisposable
    {
        private readonly IReservationManagerForOpenGE _reservationManagerForOpenGE;
        private readonly Concurrency.Semaphore _toolsReservationSemaphore;
        private IReservation? _toolsReservation;
        private IReservation? _toolBlobsReservation;
        private bool _disposed;

        public DefaultToolManager(
            IReservationManagerForOpenGE reservationManagerForOpenGE)
        {
            _reservationManagerForOpenGE = reservationManagerForOpenGE;
            _toolsReservationSemaphore = new Concurrency.Semaphore(1);
            _toolsReservation = null;
            _toolBlobsReservation = null;
            _disposed = false;
        }

        public async Task<string> GetToolPathAsync(
            long toolXxHash64,
            string toolExecutableName,
            CancellationToken cancellationToken)
        {
            var toolsPath = await GetToolsPath(cancellationToken).ConfigureAwait(false);
            return Path.Combine(
                toolsPath,
                toolXxHash64.HexString(),
                toolExecutableName);
        }

        public async Task<QueryToolResponse> QueryToolAsync(
            QueryToolRequest request,
            CancellationToken cancellationToken)
        {
            var toolsPath = await GetToolsPath(cancellationToken).ConfigureAwait(false);

            return new QueryToolResponse
            {
                Present = Directory.Exists(Path.Combine(toolsPath, request.ToolXxHash64.HexString()))
            };
        }

        public async Task<HasToolBlobsResponse> HasToolBlobsAsync(
            HasToolBlobsRequest request,
            CancellationToken cancellationToken)
        {
            var toolBlobsPath = await GetToolBlobsPath(cancellationToken).ConfigureAwait(false);

            var requested = new HashSet<long>(request.ToolBlobs.Select(x => x.XxHash64));
            var exists = new HashSet<long>();
            foreach (var file in request.ToolBlobs)
            {
                var targetPath = Path.Combine(toolBlobsPath, file.XxHash64.HexString());
                if (File.Exists(targetPath))
                {
                    exists.Add(file.XxHash64);
                }
                else if (File.Exists(file.LocalHintPath))
                {
                    var localHintHash = (await XxHash64Helpers.HashFile(file.LocalHintPath, cancellationToken).ConfigureAwait(false)).hash;
                    if (localHintHash == file.XxHash64)
                    {
                        try
                        {
                            File.Copy(file.LocalHintPath, targetPath + ".tmp", true);
                            if ((await XxHash64Helpers.HashFile(targetPath + ".tmp", cancellationToken).ConfigureAwait(false)).hash == file.XxHash64)
                            {
                                File.Move(targetPath + ".tmp", targetPath, true);
                                exists.Add(file.XxHash64);
                            }
                        }
                        catch
                        {
                            // Unable to copy local file into place.
                        }
                    }
                }
            }
            var notExists = new HashSet<long>(requested);
            notExists.ExceptWith(exists);

            var response = new HasToolBlobsResponse();
            response.Existence.AddRange(exists.Select(x => new ToolBlobExistence
            {
                XxHash64 = x,
                Exists = true,
            }));
            response.Existence.AddRange(notExists.Select(x => new ToolBlobExistence
            {
                XxHash64 = x,
                Exists = false,
            }));
            return response;
        }

        public async Task<WriteToolBlobResponse> WriteToolBlobAsync(
            WriteToolBlobRequest initialRequest,
            IWorkerRequestStream requestStream,
            CancellationToken cancellationToken)
        {
            var toolBlobsPath = await GetToolBlobsPath(cancellationToken).ConfigureAwait(false);

            if (initialRequest.InitialOrSubsequentCase != WriteToolBlobRequest.InitialOrSubsequentOneofCase.ToolBlobXxHash64)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected first WriteToolBlobRequest to have hash"));
            }

            var targetPath = Path.Combine(toolBlobsPath, initialRequest.ToolBlobXxHash64.HexString());
            var lockPath = targetPath + ".lock";
            var temporaryPath = targetPath + ".tmp";
            long committedSize = 0;

            // When we run into scenarios where another RPC has already
            // written this file, we just need to consume all the data that
            // the caller is sending our way and then tell them that the file
            // got written.
            async Task<WriteToolBlobResponse> ConsumeAndDiscardAsync(WriteToolBlobRequest request)
            {
                committedSize += request.Data.Length;
                if (!request.FinishWrite)
                {
                    while (await requestStream.MoveNext(cancellationToken).ConfigureAwait(false))
                    {
                        committedSize += requestStream.Current.WriteToolBlob.Data.Length;

                        if (requestStream.Current.WriteToolBlob.FinishWrite)
                        {
                            break;
                        }
                    }
                }
                return new WriteToolBlobResponse
                {
                    CommittedSize = committedSize,
                };
            }

            // Obtain the lock file, which prevents any other RPC from doing work
            // with this tool blob until we're done.
            IDisposable? @lock = null;
            do
            {
                // If another RPC wrote this while we were waiting for the lock, bail.
                if (File.Exists(targetPath))
                {
                    return await ConsumeAndDiscardAsync(initialRequest).ConfigureAwait(false);
                }

                @lock = LockFile.TryObtainLock(lockPath);
                if (@lock == null)
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
            }
            while (@lock == null);
            using (@lock)
            {
                // If another RPC wrote this while we were waiting for the lock, bail.
                if (File.Exists(targetPath))
                {
                    return await ConsumeAndDiscardAsync(initialRequest).ConfigureAwait(false);
                }

                // Write the temporary file and hash at the same time.
                var xxHash64 = new XxHash64();
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var request = initialRequest;
                    while (true)
                    {
                        await stream.WriteAsync(request.Data.Memory, cancellationToken).ConfigureAwait(false);
                        committedSize += request.Data.Length;
                        xxHash64.Append(request.Data.Span);
                        if (request.FinishWrite)
                        {
                            break;
                        }
                        else
                        {
                            if (!await requestStream.MoveNext(cancellationToken).ConfigureAwait(false))
                            {
                                throw new RpcException(new Status(StatusCode.InvalidArgument, "WriteToolBlobRequest stream ended early."));
                            }
                            if (requestStream.Current.RequestCase != ExecutionRequest.RequestOneofCase.WriteToolBlob)
                            {
                                throw new RpcException(new Status(StatusCode.InvalidArgument, "WriteToolBlobRequest stream ended early."));
                            }
                            request = requestStream.Current.WriteToolBlob;
                        }
                    }

                    // Ensure that what we've written matches the hash that the caller advertised.
                    if (BitConverter.ToInt64(xxHash64.GetCurrentHash()) != initialRequest.ToolBlobXxHash64)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "The provided file stream did not hash to the provided hash"));
                    }
                }

                // The temporary file is good now. Move it into place.
                File.Move(
                    temporaryPath,
                    targetPath,
                    true);
            }

            return new WriteToolBlobResponse
            {
                CommittedSize = committedSize,
            };
        }

        public async Task<ConstructToolResponse> ConstructToolAsync(
            ConstructToolRequest request,
            CancellationToken cancellationToken)
        {
            var toolsPath = await GetToolsPath(cancellationToken).ConfigureAwait(false);
            var toolBlobsPath = await GetToolBlobsPath(cancellationToken).ConfigureAwait(false);

            var hash = request.ToolXxHash64.HexString();
            var targetPath = Path.Combine(toolsPath, hash);
            var temporaryPath = Path.Combine(toolsPath, hash + ".tmp");
            var lockPath = Path.Combine(toolsPath, hash + ".lock");

            // If another RPC assembled this tool since tool synchronisation
            // started, return immediately.
            if (Directory.Exists(targetPath))
            {
                return new ConstructToolResponse
                {
                    ToolXxHash64 = request.ToolXxHash64
                };
            }

            // Obtain the lock file, which prevents any other RPC from doing work
            // with this tool until we're done.
            IDisposable? @lock = null;
            do
            {
                // If another RPC assembled this tool while we were waiting for
                // the lock, bail.
                if (Directory.Exists(targetPath))
                {
                    return new ConstructToolResponse
                    {
                        ToolXxHash64 = request.ToolXxHash64
                    };
                }

                @lock = LockFile.TryObtainLock(lockPath);
                if (@lock == null)
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
            }
            while (@lock == null);
            using (@lock)
            {
                // Create a directory for us to layout the tool.
                if (Directory.Exists(temporaryPath))
                {
                    DeleteRecursive(temporaryPath);
                }
                Directory.CreateDirectory(temporaryPath);

                // Construct the tool layout based on the request.
                foreach (var pathToHash in request.UnixRelativePathToToolBlobXxHash64)
                {
                    var directoryName = Path.GetDirectoryName(pathToHash.Key);
                    if (!string.IsNullOrWhiteSpace(directoryName))
                    {
                        Directory.CreateDirectory(Path.Combine(temporaryPath, directoryName));
                    }
                    if (!File.Exists(Path.Combine(toolBlobsPath, pathToHash.Value.HexString())))
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Missing tool blob for tool construction!"));
                    }
                    File.Copy(
                        Path.Combine(toolBlobsPath, pathToHash.Value.HexString()),
                        Path.Combine(temporaryPath, pathToHash.Key),
                        true);
                }

                // @todo: Verify that the layout matches the hash.

                // Move the directory into place.
                Directory.Move(temporaryPath, targetPath);

                return new ConstructToolResponse
                {
                    ToolXxHash64 = request.ToolXxHash64
                };
            }
        }

        private static void DeleteRecursive(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                // Try and remove "Read Only" flags on files and directories.
                foreach (var entry in Directory.GetFileSystemEntries(
                    path,
                    "*",
                    new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = true
                    }))
                {
                    var attrs = File.GetAttributes(entry);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                    {
                        attrs ^= FileAttributes.ReadOnly;
                        File.SetAttributes(entry, attrs);
                    }
                }

                // Now try to delete again.
                Directory.Delete(path, true);
            }
        }

        private async Task<string> GetToolsPath(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultToolManager));
            }
            if (_toolsReservation != null)
            {
                return _toolsReservation.ReservedPath;
            }
            await _toolsReservationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DefaultToolManager));
                }
                if (_toolsReservation != null)
                {
                    return _toolsReservation.ReservedPath;
                }
                _toolsReservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync("Tools").ConfigureAwait(false);
                return _toolsReservation.ReservedPath;
            }
            finally
            {
                _toolsReservationSemaphore.Release();
            }
        }

        private async Task<string> GetToolBlobsPath(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultToolManager));
            }
            if (_toolBlobsReservation != null)
            {
                return _toolBlobsReservation.ReservedPath;
            }
            await _toolsReservationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DefaultToolManager));
                }
                if (_toolBlobsReservation != null)
                {
                    return _toolBlobsReservation.ReservedPath;
                }
                _toolBlobsReservation = await _reservationManagerForOpenGE.ReservationManager.ReserveAsync("ToolBlobs").ConfigureAwait(false);
                return _toolBlobsReservation.ReservedPath;
            }
            finally
            {
                _toolsReservationSemaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _toolsReservationSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_toolBlobsReservation != null)
                {
                    await _toolBlobsReservation.DisposeAsync().ConfigureAwait(false);
                }
                if (_toolsReservation != null)
                {
                    await _toolsReservation.DisposeAsync().ConfigureAwait(false);
                }
                _disposed = true;
            }
            finally
            {
                _toolsReservationSemaphore.Release();
            }
        }
    }
}
