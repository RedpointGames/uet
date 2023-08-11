namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Grpc.Core;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Core.ReadableStream;
    using Redpoint.OpenGE.Core.WritableStream;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.IO.Compression;
    using System.Threading.Tasks;

    internal class DefaultBlobSynchroniser : IBlobSynchroniser
    {
        public async Task<InputFilesByBlobXxHash64> SynchroniseInputBlobs(
            IWorkerCore workerCore, 
            RemoteTaskDescriptor remoteTaskDescriptor, 
            CancellationToken cancellationToken)
        {
            var inputFilesByBlobXxHash64 = new InputFilesByBlobXxHash64();

            // Hash all of the content that we need on the remote.
            var pathsToContentHashes = new ConcurrentDictionary<string, long>();
            var contentHashesToContent = new ConcurrentDictionary<long, BlobInfo>();
            await Parallel.ForEachAsync(
                remoteTaskDescriptor.InputsByPathOrContent.AbsolutePaths.ToAsyncEnumerable(),
                cancellationToken,
                async (absolutePath, cancellationToken) =>
                {
                    var (contentHash, contentLengthBytes) = await XxHash64Helpers.HashFile(absolutePath, cancellationToken);
                    pathsToContentHashes[absolutePath] = contentHash;
                    contentHashesToContent[contentHash] = new BlobInfo
                    {
                        Path = absolutePath,
                        Content = null,
                        ByteLength = contentLengthBytes,
                    };
                });
            foreach (var entry in remoteTaskDescriptor.InputsByPathOrContent.AbsolutePathsToVirtualContent)
            {
                var (contentHash, contentLengthBytes) = XxHash64Helpers.HashString(entry.Value);
                pathsToContentHashes[entry.Key] = contentHash;
                contentHashesToContent[contentHash] = new BlobInfo
                {
                    Path = null,
                    Content = entry.Value,
                    ByteLength = contentLengthBytes,
                };
                inputFilesByBlobXxHash64.AbsolutePathsToBlobs[entry.Key] = contentHash;
                inputFilesByBlobXxHash64.AbsolutePathsToVirtualContent.Add(entry.Key);
            }
            foreach (var kv in contentHashesToContent)
            {
                // We've already added virtualised paths to the AbsolutePathsToBlobs above, so no
                // need to handle that here. We just can't access AbsolutePathsToBlobs inside the
                // Parallel.ForEachAsync used for file hashing.
                if (kv.Value.Path != null)
                {
                    inputFilesByBlobXxHash64.AbsolutePathsToBlobs[kv.Value.Path] = kv.Key;
                }
            }

            // What blobs are we missing on the remote?
            var queryMissingBlobsRequest = new QueryMissingBlobsRequest();
            queryMissingBlobsRequest.BlobXxHash64.AddRange(pathsToContentHashes.Values);
            await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
            {
                QueryMissingBlobs = queryMissingBlobsRequest,
            });
            var response = await workerCore.Request.GetNextAsync();
            if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.QueryMissingBlobs)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a QueryMissingBlobsResponse."));
            }
            if (response.QueryMissingBlobs.MissingBlobXxHash64.Count == 0)
            {
                // We don't have any blobs to transfer.
                return inputFilesByBlobXxHash64;
            }

            // Create a stream from the content blobs, and then copy from that stream
            // through the compressor stream, and then read chunks from that stream
            // and send them to the server.
            await using (var destination = new SendCompressedBlobsWritableBinaryChunkStream(
                workerCore.Request.RequestStream))
            {
                await using (var compressor = new BrotliStream(destination, CompressionMode.Compress))
                {
                    using (var source = new SequentialVersion1Encoder(
                        contentHashesToContent,
                        response.QueryMissingBlobs.MissingBlobXxHash64))
                    {
                        await source.CopyToAsync(compressor, cancellationToken);
                    }
                }
            }
            response = await workerCore.Request.GetNextAsync();
            if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.SendCompressedBlobs)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a SendCompressedBlobsResponse."));
            }

            // And now we're done.
            return inputFilesByBlobXxHash64;
        }

        public async Task SynchroniseOutputBlobs(
            IWorkerCore workerCore,
            RemoteTaskDescriptor remoteTaskDescriptor,
            ExecuteTaskResponse finalExecuteTaskResponse,
            CancellationToken cancellationToken)
        {
            // Check that the final response is what we wanted.
            if (finalExecuteTaskResponse.Response.DataCase != ProcessResponse.DataOneofCase.ExitCode)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected the final response from an ExecuteTask to be an ExitCode."));
            }

            // Make a list of files that we need. The files either need to be
            // missing, or not match the hashes that the worker is telling us it
            // has.
            var hashesToPull = new ConcurrentDictionary<long, bool>();
            var filesToPull = new ConcurrentDictionary<string, long>();
            await Parallel.ForEachAsync(
                remoteTaskDescriptor.OutputAbsolutePaths.ToAsyncEnumerable(),
                cancellationToken,
                async (absolutePath, cancellationToken) =>
                {
                    if (!finalExecuteTaskResponse.OutputAbsolutePathsToBlobXxHash64.TryGetValue(
                        absolutePath,
                        out var blobXxHash64))
                    {
                        // @todo: Should this be an error?
                        return;
                    }
                    if (!File.Exists(absolutePath))
                    {
                        hashesToPull[blobXxHash64] = true;
                        filesToPull[absolutePath] = blobXxHash64;
                    }
                    else
                    {
                        var currentHash = (await XxHash64Helpers.HashFile(absolutePath, cancellationToken)).hash;
                        if (currentHash != blobXxHash64)
                        {
                            hashesToPull[blobXxHash64] = true;
                            filesToPull[absolutePath] = blobXxHash64;
                        }
                    }
                });
            var hashesToFiles = new Dictionary<long, List<string>>();
            foreach (var kv in filesToPull)
            {
                if (!hashesToFiles.ContainsKey(kv.Value))
                {
                    hashesToFiles[kv.Value] = new List<string>();
                }
                hashesToFiles[kv.Value].Add(kv.Key);
            }

            // Pull the blobs from the remote.
            var receiveOutputBlobsRequest = new ReceiveOutputBlobsRequest();
            receiveOutputBlobsRequest.BlobXxHash64.AddRange(hashesToPull.Keys);
            await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
            {
                ReceiveOutputBlobs = receiveOutputBlobsRequest
            });
            var currentStreams = new List<FileStream>();
            long currentBytesRemaining = 0;
            try
            {
                using (var destination = new SequentialVersion1Decoder(
                    (blobHash, blobLength) =>
                    {
                        foreach (var currentStream in currentStreams)
                        {
                            currentStream.Flush();
                            currentStream.Dispose();
                        }
                        currentStreams.Clear();
                        foreach (var absolutePath in hashesToFiles[blobHash])
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                            currentStreams.Add(new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None));
                        }
                        currentBytesRemaining = blobLength;
                    },
                    (blobHash, blobOffset, buffer, bufferOffset, bufferCount) =>
                    {
                        // @note: We don't seek the file streams because we know we're writing sequentially.
                        if (currentStreams.Count == 1)
                        {
                            currentStreams[0].Write(buffer, bufferOffset, bufferCount);
                        }
                        else if (currentStreams.Count > 1)
                        {
                            Parallel.ForEach(
                                currentStreams,
                                (currentStream) => currentStream.Write(buffer, bufferOffset, bufferCount));
                        }
                        currentBytesRemaining -= bufferCount;
                        if (currentBytesRemaining < 0)
                        {
                            throw new InvalidOperationException();
                        }
                        else if (currentBytesRemaining == 0)
                        {
                            foreach (var currentStream in currentStreams)
                            {
                                currentStream.Flush();
                                currentStream.Dispose();
                            }
                            currentStreams.Clear();
                        }
                    }))
                {
                    using (var source = new ReceiveOutputBlobsReadableBinaryChunkStream(workerCore.Request))
                    {
                        using (var decompressor = new BrotliStream(source, CompressionMode.Decompress))
                        {
                            await decompressor.CopyToAsync(destination);
                        }
                    }
                }
            }
            finally
            {
                foreach (var currentStream in currentStreams)
                {
                    currentStream.Flush();
                    currentStream.Dispose();
                }
                currentStreams.Clear();
            }

            // And now we're done.
        }
    }
}
