namespace UET.Commands.Upgrade
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uet.CommonPaths;
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using static UET.Commands.Upgrade.UpgradeCommandImplementation;

    internal static class UpgradeCommandImplementation
    {
        internal static string? LastInstalledVersion { get; set; }

        internal static string GetAssemblyPathForVersion(string version)
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(UetPaths.UetRootPath, version, "uet.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(UetPaths.UetRootPath, version, "uet");
            }
            else if (OperatingSystem.IsLinux())
            {
                return Path.Combine(UetPaths.UetRootPath, version, "uet");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        [DllImport("libc")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint getuid();

        internal struct UpgradeResult
        {
            public int ExitCode;
            public bool CurrentVersionWasChanged;
        }

        internal static async Task<UpgradeResult> PerformUpgradeAsync(
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory,
            ILogger logger,
            string? version,
            bool doNotSetAsCurrent,
            bool waitForNetwork,
            CancellationToken cancellationToken)
        {
            var isRunningUnderWinPE = Environment.GetEnvironmentVariable("UET_RUNNING_UNDER_WINPE") == "1";

            if (string.IsNullOrWhiteSpace(version))
            {
                const string latestUrl = "https://github.com/RedpointGames/uet/releases/latest/download/package.version";

                logger.LogInformation("Checking for the latest version...");

            retryVersionFetch:
                try
                {
                    using (var client = new HttpClient())
                    {
                        version = (await client.GetStringAsync(new Uri(latestUrl), cancellationToken).ConfigureAwait(false)).Trim();
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.Message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase) &&
                    (isRunningUnderWinPE || waitForNetwork))
                {
                    logger.LogInformation("Unable to check for latest version; waiting on Internet connection to come up...");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    goto retryVersionFetch;
                }

                if (string.IsNullOrWhiteSpace(version))
                {
                    logger.LogError("Could not fetch latest version.");
                    return new UpgradeResult
                    {
                        ExitCode = 1,
                        CurrentVersionWasChanged = false,
                    };
                }
            }

            string downloadUrl;
            string baseFolder;
            string filename;
            if (OperatingSystem.IsWindows())
            {
                downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/uet.exe";
                baseFolder = UetPaths.UetRootPath;
                filename = "uet.exe";
            }
            else if (OperatingSystem.IsMacOS())
            {
                downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/uet";
                baseFolder = UetPaths.UetRootPath;
                filename = "uet";
            }
            else if (OperatingSystem.IsLinux())
            {
                downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/uet.linux";
                baseFolder = UetPaths.UetRootPath;
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
                return new UpgradeResult
                {
                    ExitCode = 1,
                    CurrentVersionWasChanged = false,
                };
            }

            // The special "BleedingEdge" mode needs to know what version was actually the
            // latest at startup so it can re-invoke if necessary.
            LastInstalledVersion = version;

            if (OperatingSystem.IsLinux() && !Directory.Exists(baseFolder) && getuid() != 0)
            {
                logger.LogError($"On Linux, creating the initial '{baseFolder}' directory requires root privileges. This operation may fail if you have not run it as 'sudo uet upgrade'.");
                return new UpgradeResult
                {
                    ExitCode = 1,
                    CurrentVersionWasChanged = false,
                };
            }

            void CreateDirectoryWorldWritable(string directory)
            {
                Directory.CreateDirectory(directory);
                if (OperatingSystem.IsLinux())
                {
                    // On Linux, we must make /opt/UET world-writable so it can be upgraded as users.
                    try
                    {
                        File.SetUnixFileMode(
                            directory,
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead |
                            UnixFileMode.GroupWrite |
                            UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead |
                            UnixFileMode.OtherWrite |
                            UnixFileMode.OtherExecute);
                    }
                    catch
                    {
                    }
                }
            }

            CreateDirectoryWorldWritable(baseFolder);
            CreateDirectoryWorldWritable(Path.Combine(baseFolder, version));

            if (!File.Exists(Path.Combine(baseFolder, version, filename)))
            {
                logger.LogInformation($"Downloading {version}...");
                using (var client = new HttpClient())
                {
                retryDownload:
                    try
                    {
                        using (var target = new FileStream(Path.Combine(baseFolder, version, filename + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var response = await client.GetAsync(
                                new Uri(downloadUrl),
                                HttpCompletionOption.ResponseHeadersRead,
                                cancellationToken).ConfigureAwait(false);
                            response.EnsureSuccessStatusCode();
                            using (var stream = new PositionAwareStream(
                                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
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
                                        cts.Token).ConfigureAwait(false);
                                }, cancellationToken);

                                await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);

                                cts.Cancel();
                                try
                                {
                                    await monitorTask.ConfigureAwait(false);
                                }
                                catch (OperationCanceledException) { }
                            }
                        }
                    }
                    catch (HttpRequestException ex) when (
                        ex.Message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase) &&
                        (isRunningUnderWinPE || waitForNetwork))
                    {
                        logger.LogInformation("Unable to download UET; waiting on Internet connection to come up...");
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        goto retryDownload;
                    }
                }
                Console.WriteLine();
                File.Move(Path.Combine(baseFolder, version, filename + ".tmp"), Path.Combine(baseFolder, version, filename), true);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        Path.Combine(baseFolder, version, filename),
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead |
                        UnixFileMode.GroupWrite |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead |
                        UnixFileMode.OtherWrite |
                        UnixFileMode.OtherExecute);
                }

                if (doNotSetAsCurrent)
                {
                    logger.LogInformation($"UET {version} has been downloaded successfully.");
                }
            }

            if (doNotSetAsCurrent)
            {
                return new UpgradeResult
                {
                    ExitCode = 0,
                    CurrentVersionWasChanged = false,
                };
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
                catch (IOException ex) when (ex.Message.Contains("A required privilege is not held by the client", StringComparison.Ordinal))
                {
                    logger.LogWarning("You don't have permission to create symbolic links on this system. Your PATH will be set to the specific UET version instead, which will require you to restart your terminal to start using the new UET version.");
                    targetPathForPath = Path.Combine(baseFolder, version);
                }
                catch (IOException ex) when (
                    ex.Message.Contains("Incorrect function", StringComparison.Ordinal) &&
                    isRunningUnderWinPE)
                {
                    targetPathForPath = Path.Combine(baseFolder, version);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (!isRunningUnderWinPE)
                {
                    foreach (var existingPath in (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                        .Concat((Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)))
                    {
                        if (!existingPath.StartsWith(baseFolder, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var uetPath = Path.Combine(existingPath, "uet.exe");
                            if (File.Exists(uetPath))
                            {
                                logger.LogError($"An unmanaged version of UET was found on your PATH at '{uetPath}'. Remove the unmanaged UET from this directory, or remove the directory from your PATH. Leaving an unmanaged version of UET on the PATH may result in unintended behaviour.");
                            }
                        }
                    }

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
                        else if (entry.StartsWith(baseFolder, StringComparison.OrdinalIgnoreCase))
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
                    logger.LogInformation($"Running under WinPE environment; copying new uet.exe to '{Path.Combine(baseFolder, "WinPE")}' folder assuming no further upgrades will happen in this session.");
                    Directory.CreateDirectory(Path.Combine(baseFolder, "WinPE"));
                    File.Copy(Path.Combine(targetPathForPath, "uet.exe"), Path.Combine(baseFolder, "WinPE", "uet.exe"));
                }
            }
            else
            {
                foreach (var existingPath in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!existingPath.StartsWith(baseFolder, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var uetPath = Path.Combine(existingPath, "uet");
                        if (File.Exists(uetPath))
                        {
                            if (OperatingSystem.IsLinux() &&
                                (uetPath == "/bin/uet" || uetPath == "/usr/bin/uet"))
                            {
                                // Expected for /usr/bin/uet symlink.
                                continue;
                            }

                            logger.LogError($"An unmanaged version of UET was found on your PATH at '{uetPath}'. Remove the unmanaged UET from this directory, or remove the directory from your PATH. Leaving an unmanaged version of UET on the PATH may result in unintended behaviour.");
                        }
                    }
                }

                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var pathLine = $@"PATH=""{Path.Combine(baseFolder, "Current")}:$PATH""";
                var updated = false;
                var profileFiles = new List<string>
                {
                    Path.Combine(home, ".bash_profile"),
                    Path.Combine(home, ".profile"),
                };
                if (OperatingSystem.IsMacOS())
                {
                    profileFiles.Add(Path.Combine(home, ".zprofile"));
                }
                if (OperatingSystem.IsLinux() && getuid() == 0)
                {
                    profileFiles.Add("/etc/profile");
                }
                foreach (var profilePath in profileFiles)
                {
                    if (File.Exists(profilePath))
                    {
                        var lines = (await File.ReadAllLinesAsync(profilePath, cancellationToken).ConfigureAwait(false)).ToList();
                        if (!lines.Contains(pathLine))
                        {
                            logger.LogInformation($"Adding {Path.Combine(baseFolder, "Current")} to '{profilePath}'...");
                            lines.Add(pathLine);
                            await File.WriteAllLinesAsync(profilePath, lines, cancellationToken).ConfigureAwait(false);
                            updated = true;
                        }
                    }
                }
                if (updated)
                {
                    logger.LogInformation($"Your shell profile has been updated to add {Path.Combine(baseFolder, "Current")} to your PATH. You may need to restart your terminal for the changes to take effect.");
                }

                if (OperatingSystem.IsLinux() && getuid() == 0)
                {
                    // On Linux, 'sudo uet ...' results in sudo resetting the PATH, so our modifications to .profile and /etc/profile have no effect
                    // for sudo invocations. If we are root, add a symbolic link to /usr/bin to avoid this.
                    var linkSource = "/usr/bin/uet";
                    if (!File.Exists(linkSource))
                    {
                        logger.LogInformation($"Creating a symbolic link at {linkSource} to point at the latest version of UET...");
                        try
                        {
                            File.CreateSymbolicLink(linkSource, Path.Combine(baseFolder, "Current", filename));
                        }
                        catch
                        {
                            logger.LogWarning($"Unable to create a symbolic link from {linkSource} to the current version of UET. Running 'sudo uet ...' may not work.");
                        }
                    }
                }
            }

            var currentBuildConfigPath = Path.Combine(Environment.CurrentDirectory, "BuildConfig.json");
            if (File.Exists(currentBuildConfigPath))
            {
                var document = JsonNode.Parse(await File.ReadAllTextAsync(currentBuildConfigPath, cancellationToken).ConfigureAwait(false));
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
                            await memory.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
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

            return new UpgradeResult
            {
                ExitCode = 0,
                CurrentVersionWasChanged = !wasAlreadyUpToDate,
            };
        }
    }
}
