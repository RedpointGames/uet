namespace UET.Commands.Upgrade
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal static class UpgradeCommandImplementation
    {
        internal static string? LastInstalledVersion { get; set; }

        internal static string GetAssemblyPathForVersion(string version)
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

        internal static async Task<int> PerformUpgradeAsync(
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory,
            ILogger logger,
            string? version,
            bool doNotSetAsCurrent)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                const string latestUrl = "https://dl-public.redpoint.games/file/dl-public-redpoint-games/uet/latest";

                logger.LogInformation("Checking for the latest version...");
                using (var client = new HttpClient())
                {
                    version = (await client.GetStringAsync(latestUrl)).Trim();
                }

                if (string.IsNullOrWhiteSpace(version))
                {
                    logger.LogError("Could not fetch latest version.");
                    return 1;
                }
            }

            string downloadUrl;
            string baseFolder;
            string filename;
            if (OperatingSystem.IsWindows())
            {
                downloadUrl = $"https://dl-public.redpoint.games/file/dl-public-redpoint-games/uet/{version}/windows/uet.exe";
                baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UET");
                filename = "uet.exe";
            }
            else if (OperatingSystem.IsMacOS())
            {
                downloadUrl = $"https://dl-public.redpoint.games/file/dl-public-redpoint-games/uet/{version}/macos/uet";
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
                logger.LogError($"Version '{version}' does not match version regex.");
                return 1;
            }

            // The special "BleedingEdge" mode needs to know what version was actually the
            // latest at startup so it can re-invoke if necessary.
            LastInstalledVersion = version;

            Directory.CreateDirectory(baseFolder);
            Directory.CreateDirectory(Path.Combine(baseFolder, version));

            if (!File.Exists(Path.Combine(baseFolder, version, filename)))
            {
                logger.LogInformation($"Downloading {version}...");
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
                            var progress = progressFactory.CreateProgressForStream(stream);
                            var monitorTask = Task.Run(async () =>
                            {
                                var consoleWidth = 0;
                                try
                                {
                                    consoleWidth = Console.BufferWidth;
                                }
                                catch { }

                                var monitor = monitorFactory.CreateByteBasedMonitor();
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

                if (doNotSetAsCurrent)
                {
                    logger.LogInformation($"UET {version} has been downloaded successfully.");
                }
            }

            if (doNotSetAsCurrent)
            {
                return 0;
            }

            var wasAlreadyUpToDate = true;

            if (Directory.Exists(Path.Combine(baseFolder, "Old")))
            {
                Directory.Delete(Path.Combine(baseFolder, "Old"), true);
            }

            var targetPathForPath = Path.Combine(baseFolder, "Current");
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
                logger.LogInformation($"Setting {version} as the current version on the command line...");
                try
                {
                    Directory.CreateSymbolicLink(latestLink.FullName, Path.Combine(baseFolder, version));
                }
                catch (IOException ex) when (ex.Message.Contains("A required privilege is not held by the client"))
                {
                    logger.LogWarning("You don't have permission to create symbolic links on this system. Your PATH will be set to the specific UET version instead, which will require you to restart your terminal to start using the new UET version.");
                    targetPathForPath = Path.Combine(baseFolder, version);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)!.Split(Path.PathSeparator).ToList();
                var hasPath = false;
                var didRemove = false;
                for (var i = path.Count - 1; i >= 0; i--)
                {
                    var entry = path[i];
                    if (entry == targetPathForPath)
                    {
                        // We already have it in the PATH.
                        hasPath = true;
                    }
                    else if (entry.StartsWith(baseFolder))
                    {
                        // Remove old versions from PATH.
                        path.RemoveAt(i);
                        didRemove = true;
                    }
                }
                if (!hasPath)
                {
                    logger.LogInformation($"Adding {Path.Combine(baseFolder, "Current")} to your PATH variable...");
                    path.Add(targetPathForPath);
                }
                if (!hasPath || didRemove)
                {
                    Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, path), EnvironmentVariableTarget.User);
                    logger.LogInformation($"Your PATH environment variable has been updated. You may need to restart your terminal for the changes to take effect.");
                }
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var pathLine = $@"PATH=""{Path.Combine(baseFolder, "Current")}:$PATH""";
                var updated = false;
                if (OperatingSystem.IsMacOS())
                {
                    var zprofile = Path.Combine(home, ".zprofile");
                    if (File.Exists(zprofile))
                    {
                        var lines = (await File.ReadAllLinesAsync(zprofile)).ToList();
                        if (!lines.Contains(pathLine))
                        {
                            logger.LogInformation($"Adding {Path.Combine(baseFolder, "Current")} to your .zprofile...");
                            lines.Add(pathLine);
                            await File.WriteAllLinesAsync(zprofile, lines);
                            updated = true;
                        }
                    }
                }
                var bashprofile = Path.Combine(home, ".bash_profile");
                if (File.Exists(bashprofile))
                {
                    var lines = (await File.ReadAllLinesAsync(bashprofile)).ToList();
                    if (!lines.Contains(pathLine))
                    {
                        logger.LogInformation($"Adding {Path.Combine(baseFolder, "Current")} to your .bash_profile...");
                        lines.Add(pathLine);
                        await File.WriteAllLinesAsync(bashprofile, lines);
                        updated = true;
                    }
                }
                if (updated)
                {
                    logger.LogInformation($"Your shell profile has been updated to add {Path.Combine(baseFolder, "Current")} to your PATH. You may need to restart your terminal for the changes to take effect.");
                }
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
                    else if (currentVersion?.ToString() == "BleedingEdge")
                    {
                        // We don't want to modify; we will always be using the latest
                        // version of UET for this project/plugin on-demand.
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
                    logger.LogInformation($"Setting {version} as the current version in your BuildConfig.json...");
                    var writerOptions = new JsonWriterOptions
                    {
                        Indented = true,
                        Encoder = JavaScriptEncoder.Default,
                    };
                    var serializerOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.Default,
                    };

                    // @note: We write to a memory stream first to make sure the serialization succeeds. That way if an exception is thrown,
                    // we won't have corrupted the BuildConfig.json file.
                    using (var memory = new MemoryStream())
                    {
                        using (var writer = new Utf8JsonWriter(memory, writerOptions))
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
                        memory.Seek(0, SeekOrigin.Begin);
                        using (var writer = new FileStream(currentBuildConfigPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await memory.CopyToAsync(writer);
                        }
                    }
                }
            }

            if (wasAlreadyUpToDate)
            {
                logger.LogInformation($"UET is already up-to-date.");
            }
            else
            {
                logger.LogInformation($"UET has been updated successfully.");
            }
            return 0;
        }
    }
}
