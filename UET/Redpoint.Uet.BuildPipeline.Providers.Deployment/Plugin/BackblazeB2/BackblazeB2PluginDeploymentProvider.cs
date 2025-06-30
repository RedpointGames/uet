namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2
{
    using B2Net;
    using B2Net.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.Net.Sockets;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed partial class BackblazeB2PluginDeploymentProvider : IPluginDeploymentProvider, IDynamicReentrantExecutor<BuildConfigPluginDistribution, BuildConfigPluginDeploymentBackblazeB2>
    {
        private readonly ILogger<BackblazeB2PluginDeploymentProvider> _logger;
        private readonly IMonitorFactory _monitorFactory;
        private readonly IProgressFactory _progressFactory;

        public BackblazeB2PluginDeploymentProvider(
            ILogger<BackblazeB2PluginDeploymentProvider> logger,
            IMonitorFactory monitorFactory,
            IProgressFactory progressFactory)
        {
            _logger = logger;
            _monitorFactory = monitorFactory;
            _progressFactory = progressFactory;
        }

        public string Type => "BackblazeB2";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginDeploymentBackblazeB2;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigPluginDeploymentBackblazeB2)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var deployment in castedSettings)
            {
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {deployment.name}",
                        AgentType = deployment.manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {deployment.name}",
                        Requires = "#PackagedZip;$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<BackblazeB2PluginDeploymentProvider, BuildConfigPluginDistribution, BuildConfigPluginDeploymentBackblazeB2>(
                            this,
                            context,
                            $"{deployment.name}".Replace(" ", ".", StringComparison.Ordinal),
                            deployment.settings,
                            new Dictionary<string, string>
                            {
                                { "ProjectRoot", "$(ProjectRoot)" },
                                { "PluginName", "$(PluginName)" },
                                { "Distribution", "$(Distribution)" },
                                { "VersionName", "$(VersionName)" },
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {deployment.name}",
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigPluginDeploymentBackblazeB2)configUnknown;

            var folderPrefix = string.Empty;
            if (!string.IsNullOrWhiteSpace(config.FolderPrefix))
            {
                folderPrefix = config.FolderPrefix;
                _logger.LogInformation($"Using folder prefix: {folderPrefix}");
            }
            else if (!string.IsNullOrWhiteSpace(config.FolderPrefixEnvVar))
            {
                var folderPrefixFromEnv = Environment.GetEnvironmentVariable(config.FolderPrefixEnvVar);
                if (!string.IsNullOrWhiteSpace(folderPrefixFromEnv))
                {
                    folderPrefix = folderPrefixFromEnv;
                    _logger.LogInformation($"Using folder prefix from environment variable: {config.FolderPrefixEnvVar}");
                }
                else
                {
                    _logger.LogWarning($"Folder prefix environment variable is empty: {config.FolderPrefixEnvVar}");
                }
            }
            if (string.IsNullOrWhiteSpace(folderPrefix))
            {
                _logger.LogError("Folder prefix is empty or unset - can not upload plugin.");
                return 1;
            }

            var b2KeyId = Environment.GetEnvironmentVariable("DL_BACKBLAZE_B2_KEY_ID");
            var b2AppKey = Environment.GetEnvironmentVariable("DL_BACKBLAZE_B2_APPLICATION_KEY");
            if (string.IsNullOrWhiteSpace(b2KeyId) || string.IsNullOrWhiteSpace(b2AppKey))
            {
                _logger.LogError("Backblaze B2 environment variables for authentication missing (expected DL_BACKBLAZE_B2_KEY_ID and DL_BACKBLAZE_B2_APPLICATION_KEY). Unable to run upload step.");
                return 1;
            }

            var client = new B2Net.B2Client(new B2Net.Models.B2Options
            {
                KeyId = b2KeyId,
                ApplicationKey = b2AppKey,
            });

            var bucket = await client.Buckets.GetByName(
                config.BucketName,
                cancellationToken).ConfigureAwait(false);
            if (bucket == null)
            {
                _logger.LogError($"Unable to find bucket named '{config.BucketName}' (maybe you can't access it with this key?)");
                return 1;
            }

            var zipPath = new FileInfo(Path.Combine(
                runtimeSettings["ProjectRoot"],
                $"{runtimeSettings["PluginName"]}-{runtimeSettings["Distribution"]}-{runtimeSettings["VersionName"]}.zip"));
            if (!zipPath.Exists)
            {
                _logger.LogError($"The packaged plugin does not exist: {zipPath.FullName}");
                return 1;
            }

            if (config.Strategy == BuildConfigPluginDeploymentBackblazeB2Strategy.Continuous)
            {
                _logger.LogInformation("Using 'continous' strategy for upload.");

                await UploadFileToBackblazeB2(
                    client,
                    bucket,
                    zipPath,
                    folderPrefix,
                    cancellationToken);

                return 0;
            }
            else if (config.Strategy == BuildConfigPluginDeploymentBackblazeB2Strategy.Channel)
            {
                _logger.LogInformation("Using 'channel' strategy for upload.");

                string channel;
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UET_CHANNEL_NAME")))
                {
                    channel = Environment.GetEnvironmentVariable("UET_CHANNEL_NAME")!;
                }
                else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI_COMMIT_TAG")))
                {
                    channel = Environment.GetEnvironmentVariable("CI_COMMIT_TAG")!;
                }
                else if (!string.IsNullOrWhiteSpace(config.DefaultChannelName))
                {
                    channel = config.DefaultChannelName;
                }
                else
                {
                    channel = "BleedingEdge";
                }

                string channelFolderPrefix = $"{folderPrefix}/{channel}";

                // Figure out the engine version from the .uplugin file in the ZIP so we can determine what files to delete.
                string? engineVersion = null;
                var engineVersionRegex = EngineVersionRegex();
                using (var archive = ZipFile.OpenRead(zipPath.FullName))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".uplugin", StringComparison.Ordinal))
                        {
                            using var stream = entry.Open();
                            var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                            if (json.RootElement.TryGetProperty("EngineVersion", out var engineVersionNode))
                            {
                                var engineVersionFull = engineVersionNode.GetString() ?? string.Empty;
                                var engineVersionMatch = engineVersionRegex.Match(engineVersionFull);
                                if (!engineVersionMatch.Success)
                                {
                                    _logger.LogError($"The engine version '{engineVersionFull}' in the .uplugin file does not match the engine version regex.");
                                    return 1;
                                }
                                engineVersion = $"{engineVersionMatch.Groups["major"].Value}.{engineVersionMatch.Groups["minor"].Value}";
                                break;
                            }
                        }
                    }
                }
                if (engineVersion == null)
                {
                    _logger.LogError($"The plugin package does not contain a .uplugin file.");
                    return 1;
                }

                _logger.LogInformation($"Uploading new file to: {channelFolderPrefix}/{engineVersion}/{zipPath.Name}");

                await UploadFileToBackblazeB2(
                    client,
                    bucket,
                    zipPath,
                    $"{channelFolderPrefix}/{engineVersion}",
                    cancellationToken);

                _logger.LogInformation($"Uploaded new file: {channelFolderPrefix}/{engineVersion}/{zipPath.Name}");

                string? startFileName = null;
                do
                {
                    var fullFileList = await client.Files.GetListWithPrefixOrDelimiter(
                        startFileName: null,
                        prefix: $"{channelFolderPrefix}/{engineVersion}/",
                        bucketId: bucket.BucketId,
                        cancelToken: cancellationToken);

                    foreach (var file in fullFileList.Files)
                    {
                        if (file.FileName.StartsWith($"{channelFolderPrefix}/{engineVersion}/", StringComparison.Ordinal) &&
                            !string.Equals(file.FileName, $"{channelFolderPrefix}/{engineVersion}/{zipPath.Name}", StringComparison.Ordinal))
                        {
                            _logger.LogInformation($"Deleting old file: {channelFolderPrefix}/{engineVersion}/{zipPath.Name}");
                            await client.Files.Delete(file.FileId, file.FileName, cancellationToken);
                        }
                    }

                    startFileName = fullFileList.Files.Count == 0
                        ? null
                        : fullFileList.NextFileName;
                }
                while (startFileName != null);

                _logger.LogInformation($"Channel upload completed successfully.");
                return 0;
            }

            _logger.LogError("Unknown deployment strategy for Backblaze B2 deployment.");
            return 1;
        }

        private async Task UploadFileToBackblazeB2(
            B2Client client,
            B2Bucket bucket,
            FileInfo zipPath,
            string folderPrefix,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            do
            {
                attempt++;
                var finalAttempt = attempt >= 10;
                try
                {
                    var uploadUrl = await client.Files.GetUploadUrl(bucket.BucketId, cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Uploading {zipPath.Name} ({zipPath.Length / 1024 / 1024} MB)...");

                    string? successfullyUploadedFileId = null;

                    using (var stream = new FileStream(
                        zipPath.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read | FileShare.Delete))
                    {
                        // Start monitoring.
                        var cts = new CancellationTokenSource();
                        var progress = _progressFactory.CreateProgressForStream(stream);
                        var monitorTask = Task.Run(async () =>
                        {
                            var monitor = _monitorFactory.CreateByteBasedMonitor();
                            await monitor.MonitorAsync(
                                progress,
                                SystemConsole.ConsoleInformation,
                                SystemConsole.WriteProgressToConsole,
                                cts.Token).ConfigureAwait(false);
                        }, cancellationToken);

                        // Upload the file.
                        try
                        {
                            B2File? file = await client.Files.Upload(
                                fileDataWithSHA: stream,
                                fileName: $"{folderPrefix}/{zipPath.Name}",
                                uploadUrl: uploadUrl,
                                contentType: "application/zip",
                                autoRetry: false,
                                bucketId: bucket.BucketId,
                                fileInfo: null,
                                dontSHA: true,
                                cancellationToken).ConfigureAwait(false);
                            successfullyUploadedFileId = file.FileId;
                        }
                        finally
                        {
                            // Stop monitoring.
                            await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                        }

                        _logger.LogInformation($"Friendly URL on Backblaze B2: https://f002.backblazeb2.com/file/{bucket.BucketName}/{folderPrefix}/{zipPath.Name}");
                    }

                    foreach (var version in (await client.Files.GetVersionsWithPrefixOrDelimiter(
                        startFileName: $"{folderPrefix}/{zipPath.Name}",
                        maxFileCount: 100,
                        prefix: $"{folderPrefix}/{zipPath.Name}",
                        bucketId: bucket.BucketId,
                        cancelToken: cancellationToken)).Files)
                    {
                        if (version.FileId != successfullyUploadedFileId &&
                            version.FileName == $"{folderPrefix}/{zipPath.Name}")
                        {
                            _logger.LogInformation($"Deleting old file version: {version.FileId}");
                            await client.Files.Delete(version.FileId, version.FileName, cancellationToken);
                        }
                    }

                    _logger.LogInformation("Successfully uploaded file to Backblaze B2.");
                    return;
                }
                catch (B2Exception ex) when (ex.Message.Contains("no tomes available", StringComparison.Ordinal) || ex.Message.Contains("incident id", StringComparison.Ordinal))
                {
                    if (finalAttempt)
                    {
                        throw;
                    }
                    else
                    {
                        if (SystemConsole.ConsoleWidth.HasValue)
                        {
                            Console.WriteLine();
                        }
                        _logger.LogWarning($"Temporary issue with Backblaze B2 while uploading. Retrying in {attempt} {(attempt == 1 ? "second" : "seconds")}...");
                        await Task.Delay(attempt * 1000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }
                catch (HttpRequestException ex) when (ex.InnerException is IOException ioEx && ioEx.InnerException is SocketException soEx && soEx.SocketErrorCode == SocketError.ConnectionReset)
                {
                    if (finalAttempt)
                    {
                        throw;
                    }
                    else
                    {
                        if (SystemConsole.ConsoleWidth.HasValue)
                        {
                            Console.WriteLine();
                        }
                        _logger.LogWarning($"HTTP stream closed by Backblaze B2. Retrying in {attempt} {(attempt == 1 ? "second" : "seconds")}...");
                        await Task.Delay(attempt * 1000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                }
            }
            while (true);
        }

        [GeneratedRegex("^(?<major>[0-9]+)\\.(?<minor>[0-9]+)\\.(?<patch>[0-9]+)$")]
        private static partial Regex EngineVersionRegex();
    }
}
