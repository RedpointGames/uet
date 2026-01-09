namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using k8s.Autorest;
    using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultFileTransferClient : IFileTransferClient
    {
        private readonly IDurableOperation _durableOperation;
        private readonly ILogger<DefaultFileTransferClient> _logger;
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;

        public DefaultFileTransferClient(
            IDurableOperation durableOperation,
            ILogger<DefaultFileTransferClient> logger,
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory)
        {
            _durableOperation = durableOperation;
            _logger = logger;
            _progressFactory = progressFactory;
            _monitorFactory = monitorFactory;
        }

        public async Task DownloadFilesAsync(
            Dictionary<Uri, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default)
        {
            var disposeClient = false;
            if (client == null)
            {
                client = new HttpClient();
                disposeClient = true;
            }
            try
            {
                var filesToReplace = new HashSet<string>();

                foreach (var mapping in sourceToTargetMappings)
                {
                    var fetched = await _durableOperation.DurableOperationAsync(
                        async cancellationToken =>
                        {
                            var existingHash = string.Empty;
                            var newHash = string.Empty;
                            if (File.Exists(mapping.Value))
                            {
                                using (var fileReadStream = new FileStream(mapping.Value, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    existingHash = $"sha256:{Hash.Sha256AsHexString(fileReadStream)}";
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(existingHash))
                            {
                                _logger.LogInformation($"Checking if {mapping.Value} needs to be downloaded...");
                                using var headTimerCts = new CancellationTokenSource(5000);
                                using var headCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headTimerCts.Token);
                                using (var headResponse = await client.SendAsync(
                                    new HttpRequestMessage
                                    {
                                        RequestUri = mapping.Key,
                                        Method = HttpMethod.Head,
                                    },
                                    HttpCompletionOption.ResponseHeadersRead,
                                    headCts.Token))
                                {
                                    headResponse.EnsureSuccessStatusCode();
                                    if (headResponse.Headers.TryGetValues("Content-Hash", out var newHashes))
                                    {
                                        newHash = newHashes.FirstOrDefault() ?? string.Empty;
                                    }
                                }
                            }

                            if (newHash != existingHash || string.IsNullOrWhiteSpace(existingHash))
                            {
                                _logger.LogInformation($"Downloading {mapping.Value}...");
                                using (var fileStream = new FileStream($"{mapping.Value}.tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                                {
                                    using var response = await client.GetAsync(
                                        mapping.Key,
                                        HttpCompletionOption.ResponseHeadersRead,
                                        cancellationToken);
                                    var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                                    using (var positionAwareStream = new PositionAwareStream(
                                        responseStream,
                                        response.Content.Headers.ContentLength!.Value))
                                    {
                                        using (var stream = new StallDetectionStream(
                                            positionAwareStream,
                                            TimeSpan.FromSeconds(5)))
                                        {
                                            var cts = new CancellationTokenSource();
                                            var progress = _progressFactory.CreateProgressForStream(stream);
                                            var monitorTask = Task.Run(
                                                async () =>
                                                {
                                                    var monitor = _monitorFactory.CreateByteBasedMonitor();
                                                    await monitor.MonitorAsync(
                                                        progress,
                                                        SystemConsole.ConsoleInformation,
                                                        SystemConsole.WriteProgressToConsole,
                                                        cts.Token);
                                                }, cts.Token);

                                            await stream.CopyToAsync(fileStream, cancellationToken);

                                            await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                                        }
                                    }

                                    if (response.Content.Headers.TryGetValues("Content-Hash", out var contentHashes))
                                    {
                                        fileStream.Seek(0, SeekOrigin.Begin);

                                        var hash = Hash.Sha256AsHexString(fileStream);
                                        if (contentHashes.FirstOrDefault() != $"sha256:{hash}")
                                        {
                                            throw new DownloadedFileHashInvalidException();
                                        }
                                    }
                                }

                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        },
                        cancellationToken);
                    if (fetched)
                    {
                        filesToReplace.Add(mapping.Value);
                    }
                }
                foreach (var file in filesToReplace)
                {
                    _logger.LogInformation($"Finalising download of {file}...");
                    File.Move(
                        $"{file}.tmp",
                        file,
                        true);
                }
            }
            finally
            {
                if (disposeClient)
                {
                    client.Dispose();
                }
            }
        }

        public Task DownloadFilesAsync(
            Uri sourceBaseUri,
            Dictionary<string, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default)
        {
            var newMappings = new Dictionary<Uri, string>();
            foreach (var kv in sourceToTargetMappings)
            {
                newMappings[new(sourceBaseUri.ToString().TrimEnd('/') + "/" + kv.Key)] = kv.Value;
            }
            return DownloadFilesAsync(
                newMappings,
                client,
                cancellationToken);
        }

        public Task DownloadFilesAsync(
            Uri sourceBaseUri,
            string targetBasePath,
            Dictionary<string, string> sourceToTargetMappings,
            HttpClient? client = null,
            CancellationToken cancellationToken = default)
        {
            var newMappings = new Dictionary<Uri, string>();
            foreach (var kv in sourceToTargetMappings)
            {
                newMappings[new(sourceBaseUri.ToString().TrimEnd('/') + "/" + kv.Key)] = Path.Combine(targetBasePath, kv.Value);
            }
            return DownloadFilesAsync(
                newMappings,
                client,
                cancellationToken);
        }

        public Task DownloadFileAsync(
            Uri source,
            string target,
            HttpClient? client = null,
            CancellationToken cancellationToken = default)
        {
            return DownloadFilesAsync(
                new Dictionary<Uri, string> { { source, target } },
                client,
                cancellationToken);
        }

        public async Task UploadFileAsync(
            string source,
            Uri target,
            HttpClient client,
            CancellationToken cancellationToken = default)
        {
            await _durableOperation.DurableOperationAsync(
                async cancellationToken =>
                {
                    using (var fileReadStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileReadStream.Seek(0, SeekOrigin.Begin);
                        var localHash = $"sha256:{Hash.Sha256AsHexString(fileReadStream)}";

                        _logger.LogInformation($"Checking if {source} needs to be uploaded...");
                        {
                            using var headTimerCts = new CancellationTokenSource(5000);
                            using var headCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headTimerCts.Token);
                            using (var headResponse = await client.SendAsync(
                                new HttpRequestMessage
                                {
                                    RequestUri = target,
                                    Headers =
                                    {
                                        { "Intent", "upload" },
                                        { "Content-Hash", localHash },
                                    },
                                    Method = HttpMethod.Head,
                                },
                                HttpCompletionOption.ResponseHeadersRead,
                                headCts.Token))
                            {
                                if (headResponse.StatusCode == HttpStatusCode.NotModified)
                                {
                                    return;
                                }
                            }
                        }

                        _logger.LogInformation($"Uploading {source}...");
                        fileReadStream.Seek(0, SeekOrigin.Begin);

                        using (var positionAwareStream = new PositionAwareStream(
                            fileReadStream,
                            fileReadStream.Length))
                        {
                            using (var stream = new StallDetectionStream(
                                positionAwareStream,
                                TimeSpan.FromSeconds(5)))
                            {
                                var cts = new CancellationTokenSource();
                                var progress = _progressFactory.CreateProgressForStream(stream);
                                var monitorTask = Task.Run(
                                    async () =>
                                    {
                                        var monitor = _monitorFactory.CreateByteBasedMonitor();
                                        await monitor.MonitorAsync(
                                            progress,
                                            SystemConsole.ConsoleInformation,
                                            SystemConsole.WriteProgressToConsole,
                                            cts.Token);
                                    }, cts.Token);

                                try
                                {
                                    using var uploadResponse = await client.SendAsync(
                                        new HttpRequestMessage
                                        {
                                            RequestUri = target,
                                            Headers =
                                            {
                                                { "Intent", "upload" },
                                                { "Content-Hash", localHash },
                                            },
                                            Method = HttpMethod.Put,
                                            Content = new StreamContent(stream),
                                        },
                                        HttpCompletionOption.ResponseContentRead,
                                        cancellationToken);
                                    uploadResponse.EnsureSuccessStatusCode();
                                }
                                finally
                                {
                                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                                }
                            }
                        }
                    }
                },
                cancellationToken);
        }
    }
}
