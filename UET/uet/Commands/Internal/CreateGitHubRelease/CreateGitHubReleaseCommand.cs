namespace UET.Commands.Internal.CreateGitHubRelease
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net.Http.Headers;
    using System.Reflection.Emit;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Web;

    internal class CreateGitHubReleaseCommand
    {
        internal class Options
        {
            public Option<string> Version;
            public Option<string[]> Files;

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
            }
        }

        public static Command CreateCreateGitHubReleaseCommand()
        {
            var options = new Options();
            var command = new Command("create-github-release");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateGitHubReleaseCommandInstance>(options);
            return command;
        }

        private class CreateGitHubReleaseCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateGitHubReleaseCommandInstance> _logger;
            private readonly Options _options;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IProgressFactory _progressFactory;

            public CreateGitHubReleaseCommandInstance(
                ILogger<CreateGitHubReleaseCommandInstance> logger,
                Options options,
                IMonitorFactory monitorFactory,
                IProgressFactory progressFactory)
            {
                _logger = logger;
                _options = options;
                _monitorFactory = monitorFactory;
                _progressFactory = progressFactory;
            }

            private StringContent MakeContent<T>(T value, JsonTypeInfo<T> typeInfo)
            {
                return new StringContent(
                    JsonSerializer.Serialize(
                        value,
                        typeInfo),
                    new MediaTypeHeaderValue("application/json"));
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var version = context.ParseResult.GetValueForOption(_options.Version);
                var files = (context.ParseResult.GetValueForOption(_options.Files) ?? Array.Empty<string>()).Select<string, (string name, string label, FileInfo path)>(x =>
                {
                    if (x.Contains("="))
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

                var pat = Environment.GetEnvironmentVariable("GITHUB_RELEASES_PAT");
                if (string.IsNullOrWhiteSpace(pat))
                {
                    _logger.LogError("Expected environment variable GITHUB_RELEASES_PAT to be set (this is an internal command).");
                    return 1;
                }

                if (files.Length == 0)
                {
                    _logger.LogError("Expected at least one file to include in the GitHub release.");
                    return 1;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UET", null));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                    {
                        // If the release doesn't exist, create it first.
                        ReleaseResponse release;
                        _logger.LogInformation($"Checking if there is a release for {version}...");
                        var response = await client.GetAsync($"https://api.github.com/repos/RedpointGames/uet/releases/tags/{version}");
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"Deleting existing release {version} on GitHub...");
                            release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                            response = await client.DeleteAsync(
                                $"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}",
                                context.GetCancellationToken());
                            response.EnsureSuccessStatusCode();
                        }

                        _logger.LogInformation($"Creating release {version} in draft status on GitHub...");
                        response = await client.PostAsync(
                            "https://api.github.com/repos/RedpointGames/uet/releases",
                            MakeContent(
                                new GitHubNewRelease
                                {
                                    TagName = "latest",
                                    Name = version,
                                    Body = "This is an automatic release from the GitLab build server.",
                                    Draft = true,
                                    MakeLatest = "false",
                                },
                                GitHubJsonSerializerContext.Default.GitHubNewRelease),
                            context.GetCancellationToken());
                        response.EnsureSuccessStatusCode();
                        release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.ReleaseResponse)!;

                        try
                        {
                            foreach (var file in files)
                            {
                                _logger.LogInformation($"Uploading asset {file.name} from: {file.path.FullName} ...");
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
                                            cts.Token);
                                    });

                                    // Upload the file.
                                    var content = new StreamContent(stream);
                                    content.Headers.ContentLength = file.path.Length;
                                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                    response = await client.PostAsync(
                                        $"https://uploads.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets?name={HttpUtility.UrlEncode(file.name)}",
                                        content,
                                        context.GetCancellationToken());
                                    response.EnsureSuccessStatusCode();
                                    var asset = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.AssetResponse);

                                    // Update the asset.
                                    response = await client.PatchAsync(
                                        $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{asset!.Id}",
                                        MakeContent(
                                            new GitHubPatchAsset
                                            {
                                                Name = file.name,
                                                Label = file.label,
                                            },
                                            GitHubJsonSerializerContext.Default.GitHubPatchAsset));
                                    response.EnsureSuccessStatusCode();

                                    // Stop monitoring.
                                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                                }
                            }
                        }
                        catch
                        {
                            try
                            {
                                response = await client.DeleteAsync(
                                    $"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}",
                                    context.GetCancellationToken());
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to remove partial release on creation failure: {ex}");
                            }

                            throw;
                        }

                        _logger.LogInformation($"Publishing release {version} on GitHub...");
                        response = await client.PatchAsync(
                            $"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}",
                            MakeContent(
                                new GitHubNewRelease
                                {
                                    TagName = version,
                                    Draft = false,
                                    MakeLatest = "false",
                                },
                                GitHubJsonSerializerContext.Default.GitHubNewRelease),
                            context.GetCancellationToken());
                        if (response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
                        {
                            response.EnsureSuccessStatusCode();
                        }
                    }

                    {
                        var latestDescription =
                            $"""
                            This is the latest release of UET, currently {version}. This tag is always updated to the latest version on every release, so you can download UET from the URLs below as part of CI scripts and always get the latest files.

                            The file you want to download is either:

                              - **[UET for Windows](https://github.com/RedpointGames/uet/releases/download/latest/uet.exe)**, or
                              - **[UET for macOS](https://github.com/RedpointGames/uet/releases/download/latest/uet)**.

                            The other files in this release are exist so they can be fetched on-demand by UET, or they are for specific use cases where the general UET binary is not suitable.
                            """;

                        // If the "latest" release doesn't exist, make it first.
                        ReleaseResponse release;
                        _logger.LogInformation($"Checking if there is a latest release...");
                        var response = await client.GetAsync("https://api.github.com/repos/RedpointGames/uet/releases/tags/latest");
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"Creating release 'latest' in draft status on GitHub...");
                            response = await client.PostAsync(
                                "https://api.github.com/repos/RedpointGames/uet/releases",
                                MakeContent(
                                    new GitHubNewRelease
                                    {
                                        TagName = "latest",
                                        Name = $"{version} (latest)",
                                        Body = latestDescription,
                                        Draft = false,
                                        MakeLatest = "true",
                                    },
                                    GitHubJsonSerializerContext.Default.GitHubNewRelease),
                                context.GetCancellationToken());
                            response.EnsureSuccessStatusCode();
                            release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                            _logger.LogInformation($"Created latest release with release ID {release.Id}.");
                        }
                        else
                        {
                            release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                            _logger.LogInformation($"Latest release has release ID {release.Id}.");
                        }

                        // List all of the existing release assets. We will delete these after we've done our upload.
                        response = await client.GetAsync($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets");
                        response.EnsureSuccessStatusCode();
                        var oldAssets = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.AssetResponseArray) ?? Array.Empty<AssetResponse>();
                        foreach (var oldAsset in oldAssets)
                        {
                            _logger.LogInformation($"Detected old asset {oldAsset.Id!} on the latest release.");
                        }

                        // Upload our new assets to latest, but give them unique filenames so they will never conflict.
                        var newAssets = new List<(string uploadedAs, string desiredFilename, string desiredLabel, long assetId)>();
                        try
                        {
                            foreach (var file in files)
                            {
                                _logger.LogInformation($"Uploading asset {file.name} from: {file.path.FullName} ...");
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
                                            cts.Token);
                                    });

                                    // Upload the file.
                                    var guid = Guid.NewGuid().ToString();
                                    var content = new StreamContent(stream);
                                    content.Headers.ContentLength = file.path.Length;
                                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                    response = await client.PostAsync(
                                        $"https://uploads.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets?name={HttpUtility.UrlEncode(guid)}",
                                        content,
                                        context.GetCancellationToken());
                                    response.EnsureSuccessStatusCode();
                                    var asset = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJsonSerializerContext.Default.AssetResponse);
                                    newAssets.Add((guid, file.name, file.label, asset!.Id!.Value));

                                    // Stop monitoring.
                                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                                }
                            }

                            // Rename our new assets to their desired filenames.
                            foreach (var newAsset in newAssets)
                            {
                                _logger.LogInformation($"Renaming new upload of {newAsset.desiredFilename} on latest release...");
                                var oldAsset = oldAssets.FirstOrDefault(x => x.Name == newAsset.desiredFilename);
                                if (oldAsset != null)
                                {
                                    response = await client.PatchAsync(
                                        $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id}",
                                        MakeContent(
                                            new GitHubPatchAsset
                                            {
                                                Name = $"old_{oldAsset.Id}",
                                            },
                                            GitHubJsonSerializerContext.Default.GitHubPatchAsset));
                                    response.EnsureSuccessStatusCode();
                                }
                                try
                                {
                                    response = await client.PatchAsync(
                                        $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{newAsset.assetId}",
                                        MakeContent(
                                            new GitHubPatchAsset
                                            {
                                                Name = newAsset.desiredFilename,
                                                Label = newAsset.desiredLabel,
                                            },
                                            GitHubJsonSerializerContext.Default.GitHubPatchAsset));
                                    response.EnsureSuccessStatusCode();
                                }
                                catch
                                {
                                    if (oldAsset != null)
                                    {
                                        // Failed to rename new one into place. Move the old one back.
                                        response = await client.PatchAsync(
                                            $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id}",
                                            MakeContent(
                                                new GitHubPatchAsset
                                                {
                                                    Name = oldAsset.Name,
                                                },
                                                GitHubJsonSerializerContext.Default.GitHubPatchAsset));
                                        response.EnsureSuccessStatusCode();
                                    }

                                    throw;
                                }
                            }
                        }
                        catch
                        {
                            // Remove our partially uploaded new assets.
                            foreach (var newAsset in newAssets)
                            {
                                try
                                {
                                    response = await client.DeleteAsync(
                                        $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{newAsset.assetId}",
                                        context.GetCancellationToken());
                                    response.EnsureSuccessStatusCode();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning($"Failed to remove partial asset {newAsset.assetId} on creation failure: {ex}");
                                }
                            }

                            throw;
                        }

                        // Delete the old assets on the latest release.
                        foreach (var oldAsset in oldAssets)
                        {
                            try
                            {
                                response = await client.DeleteAsync(
                                    $"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id!}",
                                    context.GetCancellationToken());
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Failed to remove old release asset after : {ex}");
                            }
                        }

                        // Update the latest release
                        _logger.LogInformation($"Updating the latest release on GitHub...");
                        response = await client.PatchAsync(
                            $"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}",
                            MakeContent(
                                new GitHubNewRelease
                                {
                                    TagName = "latest",
                                    Name = $"{version} (latest)",
                                    Body = latestDescription,
                                    Draft = false,
                                    MakeLatest = "true",
                                },
                                GitHubJsonSerializerContext.Default.GitHubNewRelease),
                            context.GetCancellationToken());
                        response.EnsureSuccessStatusCode();
                    }

                    _logger.LogInformation($"GitHub release process complete.");
                    return 0;
                }

            }
        }
    }
}
