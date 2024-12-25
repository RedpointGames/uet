namespace UET.Commands.Internal.CreateGitHubRelease
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Collections.Generic;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Web;

    internal sealed class DefaultReleaseUploader : IReleaseUploader
    {
        private readonly ILogger<DefaultReleaseUploader> _logger;
        private readonly IMonitorFactory _monitorFactory;
        private readonly IProgressFactory _progressFactory;

        public DefaultReleaseUploader(
            ILogger<DefaultReleaseUploader> logger,
            IMonitorFactory monitorFactory,
            IProgressFactory progressFactory)
        {
            _logger = logger;
            _monitorFactory = monitorFactory;
            _progressFactory = progressFactory;
        }

        private static StringContent MakeContent<T>(T value, JsonTypeInfo<T> typeInfo)
        {
            return new StringContent(
                JsonSerializer.Serialize(
                    value,
                    typeInfo),
                new MediaTypeHeaderValue("application/json"));
        }

        public async Task CreateVersionReleaseAsync(InvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client)
        {
            // If the release doesn't exist, create it first.
            ReleaseResponse release;
            _logger.LogInformation($"Checking if there is a release for {version}...");
            var response = await client.GetAsync(new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/tags/{version}")).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Deleting existing release {version} on GitHub...");
                release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                response = await client.DeleteAsync(
                    new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}"),
                    context.GetCancellationToken()).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInformation($"Creating release {version} in draft status on GitHub...");
            response = await client.PostAsync(
                new Uri("https://api.github.com/repos/RedpointGames/uet/releases"),
                MakeContent(
                    new GitHubNewRelease
                    {
                        TagName = version,
                        Name = version,
                        Body =
                        $"""
                        This is the release of UET {version}. UET employs a "rolling release" strategy, so releases are extremely frequent. You can stay up-to-date by using the `uet upgrade` command after you install it (see below).
                        
                        ## Download the latest version of UET

                        To download the latest version of UET, regardless of whether this release is the latest or not, you'll want to use one of these download links depending on your platform:
                        
                        - **[UET for Windows](https://github.com/RedpointGames/uet/releases/latest/download/uet.exe)**, or
                        - **[UET for macOS](https://github.com/RedpointGames/uet/releases/latest/download/uet)**, or
                        - **[UET for Linux](https://github.com/RedpointGames/uet/releases/latest/download/uet.linux)**.

                        The other files in this release exist so they can be fetched on-demand by UET.

                        ## Download links for CI/CD, scripting and automation
                        
                        You should not embed URLs to a specific version of UET in scripts or CI/CD. Instead, use the URL that always points to the latest version:

                        ```bash
                        # Tiny shim executables that download and cache the latest release on-demand, suitable for 
                        # always downloading at the start of CI/CD scripts.
                        https://github.com/RedpointGames/uet/releases/latest/download/uet.shim.exe      # Windows
                        https://github.com/RedpointGames/uet/releases/latest/download/uet.shim          # macOS
                        https://github.com/RedpointGames/uet/releases/latest/download/uet.shim.linux    # Linux

                        # The latest full release executables, which might be much larger.
                        https://github.com/RedpointGames/uet/releases/latest/download/uet.exe           # Windows
                        https://github.com/RedpointGames/uet/releases/latest/download/uet               # macOS
                        https://github.com/RedpointGames/uet/releases/latest/download/uet.linux         # Linux
                        ```

                        ## Installing/upgrading UET and adding it to your PATH

                        Once you've downloaded UET using the links above, you can install it system-wide by running one of the following commands depending on your platform:

                        ```bash
                        # for Windows
                        .\uet.exe upgrade

                        # for macOS or Linux
                        chmod a+x ./uet
                        ./uet upgrade
                        ```

                        After you run the commands above and re-open your terminal, you'll be able to just run `uet ...` without specifying the path to the downloaded executable.
                        
                        In addition, to upgrade UET after you've already installed it system-wide, you can run `uet upgrade` and it will update itself.
                        """,
                        Draft = true,
                        MakeLatest = "false",
                        TargetCommitish = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewRelease),
                context.GetCancellationToken()).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.ReleaseResponse)!;

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
                                cts.Token).ConfigureAwait(false);
                        });

                        // Upload the file.
                        var content = new StreamContent(stream);
                        content.Headers.ContentLength = file.path.Length;
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        response = await client.PostAsync(
                            new Uri($"https://uploads.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets?name={HttpUtility.UrlEncode(file.name)}"),
                            content,
                            context.GetCancellationToken()).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        var asset = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.AssetResponse);

                        // Update the asset.
                        response = await client.PatchAsync(
                            new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{asset!.Id}"),
                            MakeContent(
                                new GitHubPatchAsset
                                {
                                    Name = file.name,
                                    Label = file.label,
                                },
                                GitHubJsonSerializerContext.Default.GitHubPatchAsset)).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();

                        // Stop monitoring.
                        await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                try
                {
                    response = await client.DeleteAsync(
                        new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}"),
                        context.GetCancellationToken()).ConfigureAwait(false);
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
                new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}"),
                MakeContent(
                    new GitHubNewRelease
                    {
                        TagName = version,
                        Draft = false,
                        MakeLatest = "true",
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewRelease),
                context.GetCancellationToken()).ConfigureAwait(false);
            if (response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task UpdateLatestReleaseAsync(InvocationContext context, string version, (string name, string label, FileInfo path)[] files, HttpClient client)
        {
            var latestDescription =
                $"""
                # [Click here to view the latest release of UET](https://github.com/RedpointGames/uet/releases)

                **⚠️ Use of the "latest" tag is now deprecated.** You should download the latest release using the link above, which also includes the new URLs to use in CI/CD and automation scripts.
                
                ## Why is this tag deprecated?
                
                This tag was previously used as the "latest" URL for downloading UET, which helped UET perform self-updates.

                While it is still kept up-to-date so that older versions of UET can self-update to the newest version, we now use GitHub's magic "latest" URL so that the GitHub releases system can track the date of new versions correctly.

                We'll continue to update this tag until at least 1st November 2025, but after that point it may be no longer updated - if you're still running an older version of UET at that point, you can still get up-to-date by running `uet upgrade` twice.
                """;

            // If the "latest" release doesn't exist, make it first.
            ReleaseResponse release;
            _logger.LogInformation($"Checking if there is a latest release...");
            var response = await client.GetAsync(new Uri("https://api.github.com/repos/RedpointGames/uet/releases/tags/latest")).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Creating release 'latest' in draft status on GitHub...");
                response = await client.PostAsync(
                    new Uri("https://api.github.com/repos/RedpointGames/uet/releases"),
                    MakeContent(
                        new GitHubNewRelease
                        {
                            TagName = "latest",
                            Name = $"(deprecated tag)",
                            Body = latestDescription,
                            Draft = false,
                            MakeLatest = "false",
                        },
                        GitHubJsonSerializerContext.Default.GitHubNewRelease),
                    context.GetCancellationToken()).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                _logger.LogInformation($"Created latest release with release ID {release.Id}.");
            }
            else
            {
                release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.ReleaseResponse)!;
                _logger.LogInformation($"Latest release has release ID {release.Id}.");
            }

            // List all of the existing release assets. We will delete these after we've done our upload.
            response = await client.GetAsync(new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets")).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var oldAssets = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.AssetResponseArray) ?? Array.Empty<AssetResponse>();
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
                                cts.Token).ConfigureAwait(false);
                        });

                        // Upload the file.
                        var guid = Guid.NewGuid().ToString();
                        var content = new StreamContent(stream);
                        content.Headers.ContentLength = file.path.Length;
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        response = await client.PostAsync(
                            new Uri($"https://uploads.github.com/repos/RedpointGames/uet/releases/{release.Id}/assets?name={HttpUtility.UrlEncode(guid)}"),
                            content,
                            context.GetCancellationToken()).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        var asset = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync().ConfigureAwait(false), GitHubJsonSerializerContext.Default.AssetResponse);
                        newAssets.Add((guid, file.name, file.label, asset!.Id!.Value));

                        // Stop monitoring.
                        await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
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
                            new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id}"),
                            MakeContent(
                                new GitHubPatchAsset
                                {
                                    Name = $"old_{oldAsset.Id}",
                                },
                                GitHubJsonSerializerContext.Default.GitHubPatchAsset)).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                    }
                    try
                    {
                        response = await client.PatchAsync(
                            new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{newAsset.assetId}"),
                            MakeContent(
                                new GitHubPatchAsset
                                {
                                    Name = newAsset.desiredFilename,
                                    Label = newAsset.desiredLabel,
                                },
                                GitHubJsonSerializerContext.Default.GitHubPatchAsset)).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                    }
                    catch
                    {
                        if (oldAsset != null)
                        {
                            // Failed to rename new one into place. Move the old one back.
                            response = await client.PatchAsync(
                                new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id}"),
                                MakeContent(
                                    new GitHubPatchAsset
                                    {
                                        Name = oldAsset.Name,
                                    },
                                    GitHubJsonSerializerContext.Default.GitHubPatchAsset)).ConfigureAwait(false);
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
                            new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{newAsset.assetId}"),
                            context.GetCancellationToken()).ConfigureAwait(false);
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
                        new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/assets/{oldAsset.Id!}"),
                        context.GetCancellationToken()).ConfigureAwait(false);
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
                new Uri($"https://api.github.com/repos/RedpointGames/uet/releases/{release.Id}"),
                MakeContent(
                    new GitHubNewRelease
                    {
                        TagName = "latest",
                        Name = $"(deprecated tag)",
                        Body = latestDescription,
                        Draft = false,
                        MakeLatest = "false",
                    },
                    GitHubJsonSerializerContext.Default.GitHubNewRelease),
                context.GetCancellationToken()).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
    }
}
