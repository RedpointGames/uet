namespace UET.Commands.Internal.CreateGitHubRelease
{
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Web;
    using UET.Commands.Internal.GenerateJsonSchema;

    internal sealed class CreateGitHubReleaseCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<CreateGitHubReleaseCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("create-github-release");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IReleaseUploader, DefaultReleaseUploader>();
                    services.AddSingleton<ISchemaUploader, DefaultSchemaUploader>();
                    services.AddSingleton<IJsonSchemaGenerator, DefaultJsonSchemaGenerator>();
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> Version;
            public Option<string[]> Files;
            public Option<bool> SchemaOnly;

            public Options()
            {
                Version = new Option<string>(
                    "--version",
                    "The version number for the release.")
                {
                    IsRequired = true,
                };
                Files = new Option<string[]>(
                    "--file",
                    "One or more files to include in the release as assets. These can be paths on their own, or in the form of name=label=path.");
                SchemaOnly = new Option<bool>(
                    "--schema-only",
                    "Only upload the generated schema, and do not generate a release on the main repository.");
            }
        }

        private sealed class CreateGitHubReleaseCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateGitHubReleaseCommandInstance> _logger;
            private readonly Options _options;
            private readonly IReleaseUploader _releaseUploader;
            private readonly ISchemaUploader _schemaUploader;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;

            public CreateGitHubReleaseCommandInstance(
                ILogger<CreateGitHubReleaseCommandInstance> logger,
                Options options,
                IReleaseUploader releaseUploader,
                ISchemaUploader schemaUploader,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory)
            {
                _logger = logger;
                _options = options;
                _releaseUploader = releaseUploader;
                _schemaUploader = schemaUploader;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version)!;
                var files = (context.ParseResult.GetValueForOption(_options.Files) ?? Array.Empty<string>()).Select(x =>
                {
                    if (x.Contains('=', StringComparison.Ordinal))
                    {
                        var c = x.Split("=", 3);
                        return (
                            name: c[0],
                            label: c[1],
                            path: new FileInfo(c[2])
                        );
                    }
                    else
                    {
                        var fi = new FileInfo(x);
                        return (
                            name: fi.Name,
                            label: fi.Name,
                            path: fi
                        );
                    }
                }).ToArray();
                var schemaOnly = context.ParseResult.GetValueForOption(_options.SchemaOnly);

                var pat = Environment.GetEnvironmentVariable("UET_GITHUB_RELEASES_PAT");
                if (string.IsNullOrWhiteSpace(pat))
                {
                    _logger.LogError("Expected environment variable UET_GITHUB_RELEASES_PAT to be set (this is an internal command).");
                    return 1;
                }

                if (files.Length == 0 && !schemaOnly)
                {
                    _logger.LogError("Expected at least one file to include in the GitHub release.");
                    return 1;
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;

                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UET", null));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    if (!schemaOnly)
                    {
                        await _releaseUploader.CreateVersionReleaseAsync(context, version, files, client).ConfigureAwait(false);
                        await UploadFilesToR2Mirror(context, version, files, client).ConfigureAwait(false);
                    }
                    await _schemaUploader.UpdateSchemaRepositoryAsync(version, client, context.GetCancellationToken()).ConfigureAwait(false);

                    _logger.LogInformation($"GitHub release process complete.");
                    return 0;
                }
            }

            private async Task UploadFilesToR2Mirror(ICommandInvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient httpClient)
            {
                var r2AccountId = Environment.GetEnvironmentVariable("UET_R2_ACCOUNT_ID");
                var r2BucketName = Environment.GetEnvironmentVariable("UET_R2_BUCKET_NAME");
                var r2AccessKeyId = Environment.GetEnvironmentVariable("UET_R2_ACCESS_KEY_ID");
                var r2SecretAccessKey = Environment.GetEnvironmentVariable("UET_R2_SECRET_ACCESS_KEY");
                if (string.IsNullOrWhiteSpace(r2AccountId) ||
                    string.IsNullOrWhiteSpace(r2BucketName) ||
                    string.IsNullOrWhiteSpace(r2AccessKeyId) ||
                    string.IsNullOrWhiteSpace(r2SecretAccessKey))
                {
                    return;
                }

                var client = new AmazonS3Client(
                    r2AccessKeyId,
                    r2SecretAccessKey,
                    new AmazonS3Config
                    {
                        ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
                        ForcePathStyle = true,
                        AuthenticationRegion = "auto",
                    });

                foreach (var file in files)
                {
                    _logger.LogInformation($"Uploading asset {file.name} to mirror from: {file.path.FullName} ...");
                    using (var stream = new FileStream(
                        file.path.FullName,
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
                        });

                        // Upload the file.
                        await client.PutObjectAsync(
                            new PutObjectRequest
                            {
                                BucketName = r2BucketName,
                                Key = $"RedpointGames/uet/releases/download/{version}/{file.name}",
                                InputStream = stream,
                                DisablePayloadSigning = true,
                                DisableDefaultChecksumValidation = true
                            });

                        // Stop monitoring.
                        await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                    }

                    // If this is the version file, also override latest.
                    if (file.name == "package.version")
                    {
                        _logger.LogInformation($"Uploading asset {file.name} to mirror (latest) from: {file.path.FullName} ...");
                        using (var stream = new FileStream(
                            file.path.FullName,
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
                            });

                            // Upload the file.
                            await client.PutObjectAsync(
                                new PutObjectRequest
                                {
                                    BucketName = r2BucketName,
                                    Key = $"RedpointGames/uet/releases/latest/download/{file.name}",
                                    InputStream = stream,
                                    DisablePayloadSigning = true,
                                    DisableDefaultChecksumValidation = true
                                });

                            // Stop monitoring.
                            await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
