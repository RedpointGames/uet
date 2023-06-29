namespace UET.Commands.Internal.UploadToBackblazeB2
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class UploadToBackblazeB2Command
    {
        internal class Options
        {
            public Option<FileInfo> ZipPath;
            public Option<string> BucketName;
            public Option<string> FolderEnvVar;

            public Options()
            {
                ZipPath = new Option<FileInfo>("--zip-path") { IsRequired = true };
                BucketName = new Option<string>("--bucket-name") { IsRequired = true };
                FolderEnvVar = new Option<string>("--folder-env-var") { IsRequired = true };
            }
        }

        public static Command CreateUploadToBackblazeB2Command()
        {
            var options = new Options();
            var command = new Command("upload-to-backblaze-b2", "Uploads a ZIP file to Backblaze B2.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UploadToBackblazeB2CommandInstance>(options);
            return command;
        }

        private class UploadToBackblazeB2CommandInstance : ICommandInstance
        {
            private readonly ILogger<UploadToBackblazeB2CommandInstance> _logger;
            private readonly Options _options;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IProgressFactory _progressFactory;

            public UploadToBackblazeB2CommandInstance(
                ILogger<UploadToBackblazeB2CommandInstance> logger,
                Options options,
                IMonitorFactory monitorFactory,
                IProgressFactory progressFactory)
            {
                _logger = logger;
                _options = options;
                _monitorFactory = monitorFactory;
                _progressFactory = progressFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var zipPath = context.ParseResult.GetValueForOption(_options.ZipPath)!;
                var bucketName = context.ParseResult.GetValueForOption(_options.BucketName)!;
                var folderEnvVar = context.ParseResult.GetValueForOption(_options.FolderEnvVar)!;

                if (!zipPath.Exists)
                {
                    _logger.LogError("ZIP file to upload does not exist.");
                    return 1;
                }

                var folderPrefix = Environment.GetEnvironmentVariable(folderEnvVar);
                if (string.IsNullOrWhiteSpace(folderPrefix))
                {
                    _logger.LogError("Backblaze B2 environment variable for folder prefix missing. Unable to run upload step.");
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

                var bucket = (await client.Buckets.GetList(context.GetCancellationToken())).FirstOrDefault(
                    x => x.BucketName == bucketName);
                if (bucket == null)
                {
                    _logger.LogError($"Unable to find bucket named '{bucketName}' (maybe you can't access it with this key?)");
                    return 1;
                }

                var uploadUrl = await client.Files.GetUploadUrl(bucket.BucketId, context.GetCancellationToken());

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
                            cts.Token);
                    });

                    // Upload the file.
                    await client.Files.Upload(
                        fileDataWithSHA: stream,
                        fileName: $"{folderPrefix}/{zipPath.Name}",
                        uploadUrl: uploadUrl,
                        contentType: "application/zip",
                        autoRetry: false,
                        bucketId: bucket.BucketId,
                        fileInfo: null,
                        dontSHA: true,
                        context.GetCancellationToken());

                    // Stop monitoring.
                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                }

                _logger.LogInformation("Successfully uploaded file to Backblaze B2.");
                return 0;
            }
        }
    }
}
