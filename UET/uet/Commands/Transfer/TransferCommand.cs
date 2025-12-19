namespace UET.Commands.Transfer
{
    using B2Net;
    using B2Net.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    internal sealed class TransferCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<TransferCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("transfer", "Transfer a file to or from cloud storage.");
                })
            .Build();

        internal sealed class Options
        {
            public Argument<string> From;
            public Argument<string> To;

            public Option<string> KeyId;
            public Option<string> AppKey;

            public Options()
            {
                From = new Argument<string>
                {
                    Name = "from",
                    Description = "The file or URL to transfer from. Can be a local file, a Backblaze B2 address like b2://bucket-name/path, or a HTTP/HTTPS URL.",
                    Arity = ArgumentArity.ExactlyOne
                };
                To = new Argument<string>
                {
                    Name = "to",
                    Description = "The file or URL to transfer to. Can be a local file or a Backblaze B2 address like b2://bucket-name/path.",
                    Arity = ArgumentArity.ExactlyOne
                };
                KeyId = new Option<string>("--key-id")
                {
                    Description = "The Backblaze B2 application key ID.",
                };
                AppKey = new Option<string>("--app-key")
                {
                    Description = "The Backblaze B2 application key."
                };
            }
        }

        private sealed class TransferCommandInstance : ICommandInstance
        {
            private readonly ILogger<TransferCommandInstance> _logger;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly Options _options;

            public TransferCommandInstance(
                ILogger<TransferCommandInstance> logger,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                Options options)
            {
                _logger = logger;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _options = options;
            }

            private sealed record StreamContext
            {
                public required string? KeyId { get; set; }
                public required string? AppKey { get; set; }
                public HttpClient HttpClient { get; } = new HttpClient();
            }

            public sealed class RetryTransferException : Exception
            {
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var from = context.ParseResult.GetValueForArgument(_options.From);
                var to = context.ParseResult.GetValueForArgument(_options.To);

                _logger.LogInformation($"Transferring '{from}' to '{to}'...");

                var streamContext = new StreamContext
                {
                    KeyId = context.ParseResult.GetValueForOption(_options.KeyId),
                    AppKey = context.ParseResult.GetValueForOption(_options.AppKey)
                };

                if (string.IsNullOrWhiteSpace(streamContext.KeyId))
                {
                    streamContext.KeyId = Environment.GetEnvironmentVariable("DL_BACKBLAZE_B2_KEY_ID");
                }
                if (string.IsNullOrWhiteSpace(streamContext.AppKey))
                {
                    streamContext.AppKey = Environment.GetEnvironmentVariable("DL_BACKBLAZE_B2_APPLICATION_KEY");
                }

                do
                {
                    try
                    {
                        using (var fromStream = await GetFromStreamAsync(from, streamContext, context.GetCancellationToken()).ConfigureAwait(false))
                        {
                            var cts = new CancellationTokenSource();
                            var progress = _progressFactory.CreateProgressForStream(fromStream);
                            var monitorTask = Task.Run(async () =>
                            {
                                var monitor = _monitorFactory.CreateByteBasedMonitor();
                                await monitor.MonitorAsync(
                                    progress,
                                    SystemConsole.ConsoleInformation,
                                    SystemConsole.WriteProgressToConsole,
                                    cts.Token).ConfigureAwait(false);
                            });

                            await PerformUploadAsync(to, fromStream, streamContext, context.GetCancellationToken()).ConfigureAwait(false);

                            await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                        }
                        break;
                    }
                    catch (RetryTransferException)
                    {
                        if (SystemConsole.ConsoleWidth.HasValue)
                        {
                            Console.WriteLine();
                        }
                        _logger.LogWarning("Temporary network issue while transferring data. Retrying in 1 second...");
                        await Task.Delay(1000).ConfigureAwait(false);
                        continue;
                    }
                    finally
                    {
                        streamContext.HttpClient.Dispose();
                    }
                } while (true);

                _logger.LogInformation($"Transfer complete.");
                return 0;
            }

            private static async Task<Stream> GetFromStreamAsync(string from, StreamContext context, CancellationToken cancellationToken)
            {
                switch (CalculateTypeOfUrl(from))
                {
                    case "b2":
                        {
                            var client = new B2Client(new B2Net.Models.B2Options
                            {
                                KeyId = context.KeyId!,
                                ApplicationKey = context.AppKey!,
                            });
                            var url = new Uri(from);
                            var file = await client.Files.DownloadByName(url.AbsolutePath.TrimStart('/'), url.Host, cancellationToken).ConfigureAwait(false);
                            var wrapped = new PositionAwareStream(file.FileData, file.Size);
                            return wrapped;
                        }
                    case "http":
                        {
                            var response = await context.HttpClient.GetAsync(new Uri(from), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                            var wrapped = new PositionAwareStream(
                                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                                response.Content.Headers.ContentLength ?? 0);
                            return wrapped;
                        }
                    case "local":
                        {
                            return new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read);
                        }
                    default:
                        throw new NotSupportedException();
                }
            }

            private static async Task PerformUploadAsync(string to, Stream source, StreamContext context, CancellationToken cancellationToken)
            {
                switch (CalculateTypeOfUrl(to))
                {
                    case "b2":
                        var client = new B2Client(new B2Net.Models.B2Options
                        {
                            KeyId = context.KeyId!,
                            ApplicationKey = context.AppKey!,
                        });
                        var url = new Uri(to);
                        var bucketId = (await client.Buckets.GetByName(url.Host, cancellationToken).ConfigureAwait(false)).BucketId;
                        var uploadUrl = await client.Files.GetUploadUrl(bucketId, cancellationToken).ConfigureAwait(false);
                        try
                        {
                            await client.Files.Upload(
                                source,
                                url.AbsolutePath.TrimStart('/'),
                                uploadUrl,
                                "application/octet-stream",
                                false,
                                bucketId,
                                null,
                                true,
                                cancellationToken).ConfigureAwait(false);
                        }
                        catch (B2Exception ex) when (ex.Message.Contains("no tomes available", StringComparison.Ordinal))
                        {
                            throw new RetryTransferException();
                        }
                        catch (HttpRequestException ex) when (
                            ex.InnerException is IOException ioEx &&
                            ioEx.InnerException is SocketException socketEx &&
                            socketEx.SocketErrorCode == SocketError.ConnectionAborted)
                        {
                            throw new RetryTransferException();
                        }
                        return;
                    case "local":
                        using (var stream = new FileStream(to, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                        {
                            await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                        }
                        return;
                    default:
                        throw new NotSupportedException();
                }
            }

            private static string CalculateTypeOfUrl(string url)
            {
                if (url.Contains("://", StringComparison.Ordinal))
                {
                    var uri = new Uri(url);
                    switch (uri.Scheme.ToLowerInvariant())
                    {
                        case "b2":
                            return "b2";
                        case "http":
                        case "https":
                            return "http";
                        default:
                            throw new NotSupportedException($"The URL '{url}' is not recognised.");
                    }
                }
                else
                {
                    return "local";
                }
            }
        }
    }
}
