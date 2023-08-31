namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Google.Protobuf;
    using Grpc.Core;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Hashing;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IHashedToolInfo
    {
    }

    internal class DefaultToolSynchroniser : IToolSynchroniser
    {
        private readonly Dictionary<string, HashedToolInfo> _hashedToolCache = new Dictionary<string, HashedToolInfo>();
        private readonly SemaphoreSlim _toolHashingSemaphore = new SemaphoreSlim(1);

        private class HashedToolInfo : IHashedToolInfo
        {
            public required DateTimeOffset ToolLastModifiedUtc;
            public required long ToolXxHash64;
            public required string ToolExecutableName;
            public required string LocalBasePath;
            public required Dictionary<string, long> UnixRelativePathToToolBlobXxHash64;
        }

        public async Task<IHashedToolInfo> HashToolAsync(
            RemoteTaskDescriptor remoteTaskDescriptor,
            CancellationToken cancellationToken)
        {
            var path = remoteTaskDescriptor.ToolLocalAbsolutePath;

            // Compute all the files we want on the remote
            // machine and all of the hashes.
            HashedToolInfo toolInfo;
            await _toolHashingSemaphore.WaitAsync(cancellationToken);
            try
            {
                var toolLastModifiedUtc = File.GetLastWriteTimeUtc(path);
                if (!_hashedToolCache.TryGetValue(path, out toolInfo!) ||
                    toolInfo.ToolLastModifiedUtc < toolLastModifiedUtc)
                {
                    var files = new ConcurrentDictionary<string, long>();
                    var basePath = Path.GetDirectoryName(path)!;
                    var allFiles = Directory.GetFiles(
                        basePath,
                        "*",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                        });
                    await Parallel.ForEachAsync(
                        allFiles.ToAsyncEnumerable(),
                        cancellationToken,
                        async (file, cancellationToken) =>
                        {
                            var relativePath = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                            var pathHash = (await XxHash64Helpers.HashFile(file, cancellationToken)).hash;
                            files[relativePath] = pathHash;
                        });
                    var sortedHashes = files.Values.OrderBy(x => x).ToArray();
                    var toolHashBuffer = new byte[sortedHashes.Length * sizeof(long)];
                    for (int i = 0; i < sortedHashes.Length; i++)
                    {
                        Array.Copy(
                            BitConverter.GetBytes(sortedHashes[i]),
                            0,
                            toolHashBuffer,
                            i * sizeof(long),
                            sizeof(long));
                    }
                    var toolHash = BitConverter.ToInt64(XxHash64.Hash(toolHashBuffer));
                    _hashedToolCache[path] = toolInfo = new HashedToolInfo
                    {
                        ToolLastModifiedUtc = toolLastModifiedUtc,
                        LocalBasePath = basePath,
                        ToolXxHash64 = toolHash,
                        ToolExecutableName = Path.GetFileName(path),
                        UnixRelativePathToToolBlobXxHash64 = new Dictionary<string, long>(files),
                    };
                }
            }
            finally
            {
                _toolHashingSemaphore.Release();
            }

            return toolInfo;
        }

        public async Task<ToolExecutionInfo> SynchroniseToolAndGetXxHash64Async(
            ITaskApiWorkerCore workerCore,
            IHashedToolInfo toolInfoInterface,
            CancellationToken cancellationToken)
        {
            var toolInfo = (HashedToolInfo)toolInfoInterface;

            // Do we already have the tool on the remote?
            await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
            {
                QueryTool = new QueryToolRequest
                {
                    ToolXxHash64 = toolInfo.ToolXxHash64,
                }
            });
            var response = await workerCore.Request.GetNextAsync();
            if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.QueryTool)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a QueryToolResponse."));
            }
            if (response.QueryTool.Present)
            {
                // This tool already exists on the remote worker.
                return new ToolExecutionInfo
                {
                    ToolXxHash64 = toolInfo.ToolXxHash64,
                    ToolExecutableName = toolInfo.ToolExecutableName,
                };
            }

            // What blobs are we missing?
            var hasToolBlobsRequest = new HasToolBlobsRequest();
            foreach (var kv in toolInfo.UnixRelativePathToToolBlobXxHash64)
            {
                hasToolBlobsRequest.ToolBlobs.Add(new ToolBlob
                {
                    XxHash64 = kv.Value,
                    LocalHintPath = Path.Combine(toolInfo.LocalBasePath, kv.Key),
                });
            }
            await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
            {
                HasToolBlobs = hasToolBlobsRequest,
            });
            response = await workerCore.Request.GetNextAsync();
            if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.HasToolBlobs)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a HasToolBlobsResponse."));
            }
            var remoteFound = response.HasToolBlobs.Existence.Where(x => x.Exists).Select(x => x.XxHash64).ToHashSet();
            var remoteMissing = new HashSet<long>(toolInfo.UnixRelativePathToToolBlobXxHash64.Values);
            remoteMissing.ExceptWith(remoteFound);

            // Generate reverse map so we can find what we need to send.
            var reverse = new Dictionary<long, string>();
            foreach (var kv in toolInfo.UnixRelativePathToToolBlobXxHash64)
            {
                reverse[kv.Value] = kv.Key;
            }

            // Send all the remote blobs.
            foreach (var missing in remoteMissing)
            {
                var relativePath = reverse[missing];
                using (var stream = new FileStream(
                    Path.Combine(toolInfo.LocalBasePath, relativePath),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    var buffer = new byte[128 * 1024];
                    while (stream.Position != stream.Length)
                    {
                        var first = stream.Position == 0;
                        var bytesRead = stream.Read(buffer);
                        var finished = stream.Position == stream.Length;

                        var writeToolBlobRequest = new WriteToolBlobRequest();
                        if (first)
                        {
                            writeToolBlobRequest.ToolBlobXxHash64 = missing;
                        }
                        else
                        {
                            writeToolBlobRequest.Offset = stream.Position;
                        }
                        writeToolBlobRequest.FinishWrite = finished;
                        // @todo: Use internals to avoid a copy here?
                        writeToolBlobRequest.Data = ByteString.CopyFrom(buffer, 0, bytesRead);
                        await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
                        {
                            WriteToolBlob = writeToolBlobRequest,
                        });
                    }
                    response = await workerCore.Request.GetNextAsync();
                    if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.WriteToolBlob)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a WriteToolBlobResponse."));
                    }
                    if (response.WriteToolBlob.CommittedSize != stream.Length)
                    {
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not commit the entire tool blob."));
                    }
                }
            }

            // Construct the tool layout on the remote worker.
            var constructToolRequest = new ConstructToolRequest
            {
                ToolXxHash64 = toolInfo.ToolXxHash64,
            };
            foreach (var kv in toolInfo.UnixRelativePathToToolBlobXxHash64)
            {
                constructToolRequest.UnixRelativePathToToolBlobXxHash64[kv.Key] = kv.Value;
            }
            await workerCore.Request.RequestStream.WriteAsync(new ExecutionRequest
            {
                ConstructTool = constructToolRequest,
            });
            response = await workerCore.Request.GetNextAsync();
            if (response.ResponseCase != ExecutionResponse.ResponseOneofCase.ConstructTool)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not respond with a ConstructToolResponse."));
            }
            if (response.ConstructTool.ToolXxHash64 != toolInfo.ToolXxHash64)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Remote worker did not create remote tool correctly."));
            }

            return new ToolExecutionInfo
            {
                ToolXxHash64 = toolInfo.ToolXxHash64,
                ToolExecutableName = toolInfo.ToolExecutableName,
            };
        }
    }
}
