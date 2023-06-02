namespace UET.Commands.Upgrade
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal class UpgradeCommand
    {
        internal class Options
        {
            public Option<string?> Version;
            public Option<bool> DoNotSetAsCurrent;

            public Options()
            {
                Version = new Option<string?>(
                    "--version",
                    description: "The version to install. If not set, installs the latest version.");

                DoNotSetAsCurrent = new Option<bool>(
                    "--do-not-set-as-current",
                    description: "If set, then the version will only be downloaded. It won't be set as the current version to use.");
            }
        }

        public static Command CreateUpgradeCommand()
        {
            var options = new Options();
            var command = new Command("upgrade", "Upgrades your version of UET.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpgradeCommandInstance>(
                options,
                services =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            return command;
        }

        public static string GetAssemblyPathForVersion(string version)
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UET", version, "uet.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine("/Users/Shared/UET", version, "uet");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        internal class UpgradeCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<UpgradeCommandInstance> _logger;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;

            public UpgradeCommandInstance(
                Options options,
                ILogger<UpgradeCommandInstance> logger,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory)
            {
                _options = options;
                _logger = logger;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version);
                if (string.IsNullOrWhiteSpace(version))
                {
                    const string latestUrl = "https://f002.backblazeb2.com/file/dl-public-redpoint-games/uet/latest";

                    _logger.LogInformation("Checking for the latest version...");
                    using (var client = new HttpClient())
                    {
                        version = (await client.GetStringAsync(latestUrl)).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        _logger.LogError("Could not fetch latest version.");
                        return 1;
                    }
                }

                string downloadUrl;
                string baseFolder;
                string filename;
                if (OperatingSystem.IsWindows())
                {
                    downloadUrl = $"https://f002.backblazeb2.com/file/dl-public-redpoint-games/uet/{version}/windows/uet.exe";
                    baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UET");
                    filename = "uet.exe";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    downloadUrl = $"https://f002.backblazeb2.com/file/dl-public-redpoint-games/uet/{version}/macos/uet";
                    baseFolder = "/Users/Shared/UET";
                    filename = "uet";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                var versionRegex = new Regex("^[0-9\\.]+$");
                if (!versionRegex.IsMatch(version))
                {
                    _logger.LogError($"Version '{version}' does not match version regex.");
                    return 1;
                }

                Directory.CreateDirectory(baseFolder);
                Directory.CreateDirectory(Path.Combine(baseFolder, version));

                if (!File.Exists(Path.Combine(baseFolder, version, filename)))
                {
                    _logger.LogInformation($"Downloading {version}...");
                    using (var client = new HttpClient())
                    {
                        using (var target = new FileStream(Path.Combine(baseFolder, version, filename + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                            using (var stream = new PositionAwareStream(
                                await response.Content.ReadAsStreamAsync(),
                                response.Content.Headers.ContentLength!.Value))
                            {
                                var cts = new CancellationTokenSource();
                                var progress = _progressFactory.CreateProgressForStream(stream);
                                var monitorTask = Task.Run(async () =>
                                {
                                    var consoleWidth = 0;
                                    try
                                    {
                                        consoleWidth = Console.BufferWidth;
                                    }
                                    catch { }

                                    var monitor = _monitorFactory.CreateByteBasedMonitor();
                                    await monitor.MonitorAsync(
                                        progress,
                                        null,
                                        (message, count) =>
                                        {
                                            if (consoleWidth != 0)
                                            {
                                                Console.Write($"\r{message}".PadRight(consoleWidth));
                                            }
                                            else if (count % 50 == 0)
                                            {
                                                Console.WriteLine(message);
                                            }
                                        },
                                        cts.Token);
                                });

                                await stream.CopyToAsync(target);

                                cts.Cancel();
                                try
                                {
                                    await monitorTask;
                                }
                                catch (OperationCanceledException) { }
                            }
                        }
                    }
                    Console.WriteLine();
                    File.Move(Path.Combine(baseFolder, version, filename + ".tmp"), Path.Combine(baseFolder, version, filename), true);
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(
                            Path.Combine(baseFolder, version, filename),
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
                    }

                    if (context.ParseResult.GetValueForOption(_options.DoNotSetAsCurrent))
                    {
                        _logger.LogInformation($"UET {version} has been downloaded successfully.");
                    }
                }

                if (context.ParseResult.GetValueForOption(_options.DoNotSetAsCurrent))
                {
                    return 0;
                }

                var wasAlreadyUpToDate = true;

                var needsToUpdateLink = true;
                var latestLink = new DirectoryInfo(Path.Combine(baseFolder, "Current"));
                if (latestLink.Exists && latestLink.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var finalTarget = Directory.ResolveLinkTarget(latestLink.FullName, true);
                    if (finalTarget?.FullName == Path.Combine(baseFolder, version))
                    {
                        needsToUpdateLink = false;
                    }
                    else
                    {
                        Directory.Delete(latestLink.FullName);
                    }
                }
                if (needsToUpdateLink)
                {
                    wasAlreadyUpToDate = false;
                    _logger.LogInformation($"Setting {version} as the current version on the command line...");
                    Directory.CreateSymbolicLink(latestLink.FullName, Path.Combine(baseFolder, version));
                }

                var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)!.Split(Path.PathSeparator).ToList();
                if (!path.Contains(Path.Combine(baseFolder, "Current")))
                {
                    _logger.LogInformation($"Adding {Path.Combine(baseFolder, "Current")} to your PATH variable...");
                    path.Add(Path.Combine(baseFolder, "Current"));
                    Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, path), EnvironmentVariableTarget.User);
                    _logger.LogInformation($"Your PATH environment variable has been updated. You may need to restart your terminal for the changes to take effect.");
                }

                var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
                if (File.Exists(currentBuildConfigPath))
                {
                    var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath));
                    var didUpdate = false;
                    try
                    {
                        var documentObject = document!.AsObject();
                        var currentVersion = documentObject["UETVersion"];
                        if (currentVersion?.ToString() == version)
                        {
                            // No need to modify.
                        }
                        else
                        {
                            documentObject["UETVersion"] = version;
                            didUpdate = true;
                        }
                    }
                    catch
                    {
                    }
                    if (didUpdate)
                    {
                        wasAlreadyUpToDate = false;
                        _logger.LogInformation($"Setting {version} as the current version in your BuildConfig.json...");
                        var writerOptions = new JsonWriterOptions
                        {
                            Indented = true,
                        };
                        var serializerOptions = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                        };
                        using (var writer = new Utf8JsonWriter(new FileStream(currentBuildConfigPath, FileMode.Create, FileAccess.Write, FileShare.None), writerOptions))
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("UETVersion");
                            document!["UETVersion"]!.WriteTo(writer, serializerOptions);
                            foreach (var kv in document.AsObject())
                            {
                                if (kv.Key == "UETVersion")
                                {
                                    continue;
                                }
                                writer.WritePropertyName(kv.Key);
                                if (kv.Value == null)
                                {
                                    writer.WriteNullValue();
                                }
                                else
                                {
                                    kv.Value!.WriteTo(writer, serializerOptions);
                                }
                            }
                            writer.WriteEndObject();
                        }
                    }
                }

                if (wasAlreadyUpToDate)
                {
                    _logger.LogInformation($"UET is already up-to-date.");
                }
                else
                {
                    _logger.LogInformation($"UET has been updated successfully.");
                }
                return 0;
            }
        }
    }
}
