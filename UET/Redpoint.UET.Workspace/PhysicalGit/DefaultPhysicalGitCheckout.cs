namespace Redpoint.UET.Workspace.PhysicalGit
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.UET.Core;
    using Redpoint.UET.Workspace.Descriptors;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultPhysicalGitCheckout : IPhysicalGitCheckout
    {
        private readonly ILogger<DefaultPhysicalGitCheckout> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        public DefaultPhysicalGitCheckout(
            ILogger<DefaultPhysicalGitCheckout> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        private class SubmoduleDescription
        {
            public required string Id { get; set; }
            public required string Path { get; set; }
            public required string Url { get; set; }
            public required bool ExcludeOnMac { get; set; }
        }

        private async Task<IEnumerable<SubmoduleDescription>> ParseSubmodulesAsync(string path)
        {
            var results = new List<SubmoduleDescription>();
            var submoduleGroupName = string.Empty;
            var submodulePath = string.Empty;
            var submoduleUrl = string.Empty;
            var submoduleExcludeOnMac = string.Empty;
            var gitmodulesPath = Path.Combine(path, ".gitmodules");
            if (!File.Exists(gitmodulesPath))
            {
                return results;
            }
            foreach (var line in await File.ReadAllLinesAsync(gitmodulesPath))
            {
                if (line.StartsWith("[submodule \""))
                {
                    if (!string.IsNullOrWhiteSpace(submoduleGroupName) &&
                        !string.IsNullOrWhiteSpace(submodulePath) &&
                        !string.IsNullOrWhiteSpace(submoduleUrl))
                    {
                        results.Add(new SubmoduleDescription
                        {
                            Id = submoduleGroupName,
                            Path = submodulePath,
                            Url = submoduleUrl,
                            ExcludeOnMac = submoduleExcludeOnMac.Equals("true", StringComparison.InvariantCultureIgnoreCase),
                        });
                    }
                    submoduleGroupName = line.Substring("[submodule \"".Length);
                    submoduleGroupName = submoduleGroupName.Substring(0, submoduleGroupName.Length - 1);
                    submodulePath = string.Empty;
                    submoduleUrl = string.Empty;
                    submoduleExcludeOnMac = string.Empty;
                }
                else if (line.Trim().StartsWith("path = ") && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submodulePath = line.Substring(line.IndexOf("path = ") + "path = ".Length).Trim();
                }
                else if (line.Trim().StartsWith("url = ") && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submoduleUrl = line.Substring(line.IndexOf("url = ") + "url = ".Length).Trim();
                }
                else if (line.Trim().StartsWith("exclude-on-mac = ") && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submoduleExcludeOnMac = line.Substring(line.IndexOf("exclude-on-mac = ") + "exclude-on-mac = ".Length).Trim();
                }
            }
            if (!string.IsNullOrWhiteSpace(submoduleGroupName) &&
                !string.IsNullOrWhiteSpace(submodulePath) &&
                !string.IsNullOrWhiteSpace(submoduleUrl))
            {
                results.Add(new SubmoduleDescription
                {
                    Id = submoduleGroupName,
                    Path = submodulePath,
                    Url = submoduleUrl,
                    ExcludeOnMac = submoduleExcludeOnMac.Equals("true", StringComparison.InvariantCultureIgnoreCase),
                });
            }
            return results;
        }

        private IEnumerable<FileInfo> GetPluginAndProjectFiles(DirectoryInfo directory)
        {
            foreach (var file in directory.GetFiles("*.uproject", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                yield return file;
            }
            foreach (var file in directory.GetFiles("*.uplugin", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                yield return file;
            }
        }

        private DirectoryInfo GetGitBaseDirectoryForPath(FileInfo cleanFile)
        {
            var directory = cleanFile.Directory;
            while (directory != null)
            {
                if (Path.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory;
                }
                directory = directory.Parent;
            }
            throw new InvalidOperationException($"Can't figure out what .git directory {cleanFile.FullName} is located in.");
        }

        private async Task<int> FaultTolerantFetchAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var backoff = 1000;
            var attempts = 0;
            do
            {
                var exitCode = await _processExecutor.ExecuteAsync(
                    processSpecification,
                    captureSpecification,
                    cancellationToken);
                if (exitCode == 128 && attempts < 10)
                {
                    // 'git fetch' returns exit code 128 when the remote host
                    // unexpectedly disconnects. We want to handle unreliable
                    // network connections by simply retrying the 'git fetch'
                    // operation.
                    _logger.LogWarning($"'git fetch' encountered a network error while fetching commits. Retrying the fetch operation in {backoff}ms...");
                    await Task.Delay(backoff);
                    backoff *= 2;
                    if (backoff > 30000)
                    {
                        backoff = 30000;
                    }
                    attempts++;
                    continue;
                }
                else if (exitCode == 128)
                {
                    // We attempted too many times and can't continue.
                    _logger.LogError("Fault tolerant fetch ran into exit code 128 over 10 attempts, permanently failing...");
                    return exitCode;
                }

                // Some other exit code, just return.
                return exitCode;
            } while (true);
        }

        private async IAsyncEnumerable<(DirectoryInfo contentDirectory, SubmoduleDescription submodule, DirectoryInfo submoduleGitDirectory)> IterateContentBasedSubmodulesAsync(
            IReservation reservation,
            GitWorkspaceDescriptor descriptor)
        {
            foreach (var topLevelSubmodule in await ParseSubmodulesAsync(reservation.ReservedPath))
            {
                if (topLevelSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                {
                    continue;
                }

                if (topLevelSubmodule.Path.Contains("/Source/") || (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/")))
                {
                    yield return (
                        new DirectoryInfo(reservation.ReservedPath),
                        topLevelSubmodule,
                        new DirectoryInfo($"{reservation.ReservedPath}/.git/modules/{topLevelSubmodule.Id}"));
                    if (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/"))
                    {
                        foreach (var childSubmodule in await ParseSubmodulesAsync($"{reservation.ReservedPath}/{topLevelSubmodule.Path}"))
                        {
                            if (childSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                            {
                                continue;
                            }
                            yield return (
                                new DirectoryInfo($"{reservation.ReservedPath}/{topLevelSubmodule.Path}"),
                                childSubmodule,
                                new DirectoryInfo($"{reservation.ReservedPath}/.git/modules/${topLevelSubmodule.Id}/modules/{childSubmodule.Id}"));
                        }
                    }
                }
            }
        }

        private async Task CheckoutSubmoduleAsync(
            string git,
            Dictionary<string, string> gitEnvs,
            Uri mainRepositoryUrl,
            DirectoryInfo contentDirectory,
            SubmoduleDescription submodule,
            DirectoryInfo submoduleGitDirectory,
            CancellationToken cancellationToken)
        {
            int exitCode = 0;

            // Initialize the submodule if needed.
            var submoduleGitIndicatorFile = Path.Combine(contentDirectory.FullName, submodule.Path, ".git");
            var submoduleContentPath = Path.Combine(contentDirectory.FullName, submodule.Path);
            if (!Path.Exists(submoduleGitIndicatorFile))
            {
                Directory.CreateDirectory(submoduleContentPath);
                if (submoduleGitDirectory.Exists)
                {
                    var relativeModulePath = Path.GetRelativePath(
                        submoduleContentPath,
                        submoduleGitDirectory.FullName);
                    _logger.LogInformation($"Attaching submodule {submodule.Path} to {relativeModulePath}...");
                    await File.WriteAllTextAsync(submoduleGitIndicatorFile, relativeModulePath);
                }
                else
                {
                    _logger.LogInformation($"Initializing submodule {submodule.Path}...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "-C",
                                submoduleContentPath,
                                "init",
                            },
                            WorkingDirectory = submoduleContentPath,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"'git init' inside {submoduleContentPath} exited with non-zero exit code {exitCode}");
                    }
                }
            }
            else if (Directory.Exists(submoduleGitIndicatorFile))
            {
                _logger.LogInformation($"Absorbing submodule {submodule.Path}...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            contentDirectory.FullName,
                            "submodule",
                            "absorbgitdirs",
                            "--",
                            submodule.Path,
                        },
                        WorkingDirectory = contentDirectory.FullName,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git submodule absorbgitdirs' for {submodule.Path} exited with non-zero exit code {exitCode}");
                }
            }

            // Compute the commit and URL for this submodule.
            var submoduleStatusStringBuilder = new StringBuilder();
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-C",
                        contentDirectory.FullName,
                        "ls-tree",
                        "-l",
                        "HEAD",
                        submodule.Path,
                    },
                    WorkingDirectory = contentDirectory.FullName,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(submoduleStatusStringBuilder),
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git ls-tree' for {submodule.Path} exited with non-zero exit code {exitCode}");
            }
            var submoduleCommit = submoduleStatusStringBuilder.ToString().Split(" ")[2].Trim();
            var submoduleUrl = submodule.Url;
            if (submoduleUrl.StartsWith("ssh://", StringComparison.InvariantCultureIgnoreCase) ||
                submoduleUrl.StartsWith("git://", StringComparison.InvariantCultureIgnoreCase) ||
                submoduleUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
                submoduleUrl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            {
                // This is an absolute URL already. Nothing to do.
            }
            else
            {
                var uriBuilder = new UriBuilder(mainRepositoryUrl);
                var currentPathComponents = uriBuilder.Path.Split('/').ToList();
                var relativePathComponents = submoduleUrl.Split('/').ToArray();
                _logger.LogTrace($"Processing relative submodule path '{submoduleUrl}'...");
                _logger.LogTrace($"Relative path component count: {relativePathComponents.Length}");
                var assignedAbsolute = false;
                for (int i = 0; i < relativePathComponents.Length; i++)
                {
                    if (i == 0 && relativePathComponents[i] == string.Empty)
                    {
                        // This is an absolute path reset, like "/a/b/c".
                        _logger.LogTrace($"Component {i} {relativePathComponents[i]}: absolute path reset");
                        uriBuilder.Path = submoduleUrl;
                        assignedAbsolute = true;
                        break;
                    }
                    else if (relativePathComponents[i] == ".")
                    {
                        // Nothing to adjust.
                        _logger.LogTrace($"Component {i} {relativePathComponents[i]}: nothing to adjust");
                    }
                    else if (relativePathComponents[i] == "..")
                    {
                        _logger.LogTrace($"Component {i} {relativePathComponents[i]}: updir");
                        currentPathComponents.RemoveAt(currentPathComponents.Count - 1);
                    }
                    else
                    {
                        _logger.LogTrace($"Component {i} {relativePathComponents[i]}: add");
                        currentPathComponents.Add(relativePathComponents[i]);
                    }
                }
                if (!assignedAbsolute)
                {
                    uriBuilder.Path = "/" + string.Join('/', currentPathComponents).TrimStart('/');
                }
                submoduleUrl = uriBuilder.ToString();
            }

            // @note: "-C" doesn't work for submodules, but we don't need to worry about this any more because we can set the working directory via the process executor.
            var currentHeadStringBuilder = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "rev-parse",
                        "HEAD",
                    },
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(currentHeadStringBuilder),
                cancellationToken);
            var submoduleGitCheckoutPath = Path.Combine(submoduleContentPath, ".gitcheckout");
            if (currentHeadStringBuilder.ToString().Trim() == submoduleCommit)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(submoduleGitCheckoutPath))
                {
                    var lastSubmoduleCommit = (await File.ReadAllTextAsync(submoduleGitCheckoutPath)).Trim();
                    if (lastSubmoduleCommit == submoduleCommit)
                    {
                        _logger.LogInformation($"Git submodule {submoduleContentPath} already up-to-date.");
                        return;
                    }
                    else
                    {
                        _logger.LogInformation($"Submodule needs checkout because '{lastSubmoduleCommit}' (.gitcheckout) != '{submoduleCommit}'");
                    }
                }
                else
                {
                    _logger.LogInformation($"Submodule needs checkout because this file doesn't exist: '{submoduleGitCheckoutPath}'");
                }
            }
            else
            {
                _logger.LogInformation($"Submodule needs checkout because '{currentHeadStringBuilder.ToString().Trim()}' != '{submoduleCommit}'");
            }

            _logger.LogInformation($"Submodule commit: {submoduleCommit}");
            _logger.LogInformation($"Submodule URL: {submoduleUrl}");
            _logger.LogInformation($"Submodule directory: {submoduleContentPath}");
            _logger.LogInformation($"Submodule exclude on macOS: {(submodule.ExcludeOnMac ? "yes" : "no")}");

            // Check if we already have the target commit in history. If we do, skip fetch.
            var gitTypeStringBuilder = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "cat-file",
                        "-t",
                        submoduleCommit,
                    },
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeStringBuilder),
                cancellationToken);
            var gitType = gitTypeStringBuilder.ToString().Trim();
            if (gitType != "commit")
            {
                // Fetch the commit that we need.
                _logger.LogInformation($"Fetching submodule {submodule.Path} from remote server...");
                exitCode = await FaultTolerantFetchAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "fetch",
                            "-f",
                            "--recurse-submodules=no",
                            submoduleUrl,
                            $"{submoduleCommit}:FETCH_HEAD",
                        },
                        WorkingDirectory = submoduleContentPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git fetch' for {submodule.Path} exited with non-zero exit code {exitCode}");
                }

                // We don't fetch Git LFS in submodules. Git LFS should only be used in project Content folders, and projects won't have other projects inside submodules.
            }

            // Checkout the target commit.
            _logger.LogInformation($"Checking out submodule {submodule.Path} target commit {submoduleCommit}...");
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "-f",
                        submoduleCommit,
                    },
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git checkout' for {submodule.Path} exited with non-zero exit code {exitCode}");
            }

            // Write our .gitcheckout file which tells subsequent calls that we're up-to-date.
            await File.WriteAllTextAsync(submoduleGitCheckoutPath, submoduleCommit);
        }

        public async Task PrepareGitWorkspaceAsync(
            IReservation reservation,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            var git = await _pathResolver.ResolveBinaryPath("git");
            var gitEnvs = new Dictionary<string, string>
            {
                { "GIT_ASK_YESNO", "false" },
            };
            var exitCode = 0;

            // Initialize the Git repository if needed.
            if (!Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")))
            {
                _logger.LogInformation("Initializing Git repository because it doesn't already exist...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "init",
                            reservation.ReservedPath
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git init' exited with non-zero exit code {exitCode}");
                }

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            reservation.ReservedPath,
                            "config",
                            "core.symlinks",
                            "true"
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... config core.symlinks true' exited with non-zero exit code {exitCode}");
                }
            }

            // Resolve tags and refs if needed.
            var targetCommit = descriptor.RepositoryCommitOrRef;
            var targetIsPotentialAnnotatedTag = false;
            if (!new Regex("^[a-f0-9]{40}$").IsMatch(targetCommit))
            {
                _logger.LogInformation($"Resolving ref '{targetCommit}' to commit on remote Git server...");
                var resolvedRefStringBuilder = new StringBuilder();
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "ls-remote",
                            "--exit-code",
                            descriptor.RepositoryUrl,
                            targetCommit,
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(resolvedRefStringBuilder),
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git ls-remote --exit-code ...' exited with non-zero exit code {exitCode}");
                }
                if (string.IsNullOrWhiteSpace(resolvedRefStringBuilder.ToString()))
                {
                    throw new InvalidOperationException($"'git ls-remote --exit-code ...' did not return any match refs for '{targetCommit}'");
                }
                string? resolvedRef = null;
                foreach (var line in resolvedRefStringBuilder.ToString().Replace("\r\n", "\n").Split('\n'))
                {
                    var component = line.Replace("\t", " ").Split(" ")[0];
                    if (line.Contains("refs/tags/"))
                    {
                        targetIsPotentialAnnotatedTag = true;
                    }
                    resolvedRef = component.Trim();
                    break;
                }
                if (resolvedRef == null)
                {
                    throw new InvalidOperationException($"'git ls-remote --exit-code ...' did not return any match refs for '{targetCommit}'");
                }
                if (!new Regex("^[a-f0-9]{40}$").IsMatch(resolvedRef))
                {
                    throw new InvalidOperationException($"'git ls-remote --exit-code ...' returned non-SHA '{resolvedRef}' for ref '{targetCommit}'");
                }
                targetCommit = resolvedRef;
            }

            // Check if we've already got the commit checked out.
            var currentHead = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-C",
                        reservation.ReservedPath,
                        "rev-parse",
                        "HEAD"
                    },
                    WorkingDirectory = reservation.ReservedPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(currentHead),
                cancellationToken);
            if (currentHead.ToString().Trim() == targetCommit)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(Path.Combine(reservation.ReservedPath, ".gitcheckout")))
                {
                    // Just really quickly check to make sure the .git file exists in each submodule we care about. If it doesn't,
                    // then the .gitcheckout file is stale and needs to be removed.
                    var validSubmoduleLayout = true;
                    _logger.LogInformation("Quickly checking submodules...");
                    await foreach (var iter in IterateContentBasedSubmodulesAsync(reservation, descriptor))
                    {
                        var dotGitPath = Path.Combine(iter.contentDirectory.FullName, ".git");
                        if (!Path.Exists(dotGitPath))
                        {
                            _logger.LogWarning($"Missing .git file at {dotGitPath}, purging .gitcheckout...");
                            validSubmoduleLayout = false;
                            break;
                        }
                    }
                    if (validSubmoduleLayout)
                    {
                        _logger.LogInformation("Git repository already up-to-date.");
                        return;
                    }
                    else
                    {
                        File.Delete(Path.Combine(reservation.ReservedPath, ".gitcheckout"));
                        // Continue with full process...
                    }
                }
            }

            // Parse the repository URL.
            var uri = new Uri(descriptor.RepositoryUrl);

            // Check if we already have the target commit in history. If we do, skip fetch.
            var gitTypeBuilder = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-C",
                        reservation.ReservedPath,
                        "cat-file",
                        "-t",
                        targetCommit,
                    },
                    WorkingDirectory = reservation.ReservedPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                cancellationToken);
            var gitType = gitTypeBuilder.ToString().Trim();

            // If we know this is an annotated commit, resolve which commit it points to.
            if (gitType == "tag")
            {
                var targetCommitBuilder = new StringBuilder();
                _ = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            reservation.ReservedPath,
                            "rev-list",
                            "-n",
                            "1",
                            targetCommit,
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(targetCommitBuilder.ToString()))
                {
                    targetCommit = targetCommitBuilder.ToString().Trim();
                    gitTypeBuilder = new StringBuilder();
                    _ = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "-C",
                                reservation.ReservedPath,
                                "cat-file",
                                "-t",
                                targetCommit,
                            },
                            WorkingDirectory = reservation.ReservedPath,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                        cancellationToken);
                    gitType = gitTypeBuilder.ToString().Trim();
                }
            }

            // If we couldn't resolve the reference, we don't have the commit.
            if (gitType != "commit")
            {
                _logger.LogInformation("Fetching repository from remote server...");
                // Fetch the commit that we need.
                while (true)
                {
                    var fetchStringBuilder = new StringBuilder();
                    if (targetIsPotentialAnnotatedTag)
                    {
                        exitCode = await FaultTolerantFetchAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = new[]
                                {
                                    "-C",
                                    reservation.ReservedPath,
                                    "fetch",
                                    "-f",
                                    "--recurse-submodules=no",
                                    "--progress",
                                    uri.ToString(),
                                    targetCommit
                                },
                                WorkingDirectory = reservation.ReservedPath,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Sanitized,
                            cancellationToken);
                        // Now that we've fetched the potential tag, check if it really is a tag. If it is, resolve it to the commit hash instead.
                        gitTypeBuilder = new StringBuilder();
                        _ = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = new[]
                                {
                                    "-C",
                                    reservation.ReservedPath,
                                    "cat-file",
                                    "-t",
                                    targetCommit,
                                },
                                WorkingDirectory = reservation.ReservedPath,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                            cancellationToken);
                        gitType = gitTypeBuilder.ToString().Trim();
                        if (gitType == "tag")
                        {
                            var targetCommitBuilder = new StringBuilder();
                            _ = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = new[]
                                    {
                                        "-C",
                                        reservation.ReservedPath,
                                        "rev-list",
                                        "-n",
                                        "1",
                                        targetCommit,
                                    },
                                    WorkingDirectory = reservation.ReservedPath,
                                    EnvironmentVariables = gitEnvs,
                                },
                                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                                cancellationToken);
                            targetCommit = targetCommitBuilder.ToString().Trim();
                        }
                    }
                    else
                    {
                        exitCode = await FaultTolerantFetchAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = new[]
                                {
                                    "-C",
                                    reservation.ReservedPath,
                                    "fetch",
                                    "-f",
                                    "--recurse-submodules=no",
                                    "--progress",
                                    uri.ToString(),
                                    $"{targetCommit}:FETCH_HEAD"
                                },
                                WorkingDirectory = reservation.ReservedPath,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Sanitized,
                            cancellationToken);
                    }
                    if (fetchStringBuilder.ToString().Contains("fatal: early EOF"))
                    {
                        // Temporary connection issue with Git server. Retry.
                        continue;
                    }
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"'git -C ... fetch -f --recurse-submodules=no --progress ...' exited with non-zero exit code {exitCode}");
                    }
                    break;
                }

                // Fetch the LFS as well.
                _logger.LogInformation("Fetching LFS files from remote server...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            reservation.ReservedPath,
                            "lfs",
                            "fetch",
                            uri.ToString(),
                            targetCommit,
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... lfs fetch ...' exited with non-zero exit code {exitCode}");
                }
            }

            // Checkout the target commit.
            _logger.LogInformation($"Checking out target commit {targetCommit}...");
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-C",
                        reservation.ReservedPath,
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "-f",
                        targetCommit,
                    },
                    WorkingDirectory = reservation.ReservedPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken);
            if (exitCode != 0)
            {
                // Attempt to re-fetch LFS files, in case that was the error.
                _logger.LogInformation("Fetching LFS files from remote server...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            reservation.ReservedPath,
                            "lfs",
                            "fetch",
                            uri.ToString(),
                            targetCommit,
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... lfs fetch ...' exited with non-zero exit code {exitCode}");
                }

                // Re-attempt checkout...
                _logger.LogInformation($"Re-attempting check out of target commit {targetCommit}...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            reservation.ReservedPath,
                            "-c",
                            "advice.detachedHead=false",
                            "checkout",
                            "-f",
                            targetCommit,
                        },
                        WorkingDirectory = reservation.ReservedPath,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git checkout ...' exited with non-zero exit code {exitCode}");
                }
            }

            // Clean all Source, Config, Resources and Content folders so that we don't have stale files accidentally included in build steps.
            _logger.LogInformation($"Cleaning build sensitive directories...");
            if (!descriptor.IsEngineBuild)
            {
                var sensitiveDirectories = new string[] { "Source", "Config", "Resources", "Content" };
                foreach (var cleanFile in GetPluginAndProjectFiles(new DirectoryInfo(reservation.ReservedPath)))
                {
                    var baseDirectory = GetGitBaseDirectoryForPath(cleanFile);
                    var relativeBasePathToCleanDir = Path.GetRelativePath(baseDirectory.FullName, cleanFile.DirectoryName!).Replace("\\", "/").Trim('/');
                    var relativeReservedPathToBase = Path.GetRelativePath(reservation.ReservedPath, baseDirectory.FullName).Replace("\\", "/").Trim('/');
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "-C",
                                baseDirectory.FullName,
                                "ls-files",
                                "--error-unmatch",
                                $"{relativeBasePathToCleanDir}/{cleanFile.Name}",
                            },
                            WorkingDirectory = reservation.ReservedPath,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Silence,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        // This is not a tracked project/plugin, nuke the build sensitive folders directly and expect BuildGraph to either populate or download them.
                        foreach (var sensitiveDirectory in sensitiveDirectories)
                        {
                            var sensitivePath = Path.Combine(cleanFile.DirectoryName!, sensitiveDirectory);
                            if (Directory.Exists(sensitivePath))
                            {
                                _logger.LogInformation($"Nuking: ({relativeReservedPathToBase}) {relativeBasePathToCleanDir}/{sensitiveDirectory}");
                                await DirectoryAsync.DeleteAsync(sensitivePath, true);
                            }
                        }
                    }
                    else
                    {
                        // This is a tracked project/plugin, use git clean to clear out unwanted files.
                        foreach (var sensitiveDirectory in sensitiveDirectories)
                        {
                            var sensitivePath = Path.Combine(cleanFile.DirectoryName!, sensitiveDirectory);
                            if (Directory.Exists(sensitivePath))
                            {
                                _logger.LogInformation($"Cleaning: ({relativeReservedPathToBase}) {relativeBasePathToCleanDir}/{sensitiveDirectory}");
                                _ = await _processExecutor.ExecuteAsync(
                                    new ProcessSpecification
                                    {
                                        FilePath = git,
                                        Arguments = new[]
                                        {
                                            "-C",
                                            baseDirectory.FullName,
                                            "clean",
                                            "-xdff",
                                            $"{relativeBasePathToCleanDir}/{sensitiveDirectory}",
                                        },
                                        WorkingDirectory = baseDirectory.FullName,
                                        EnvironmentVariables = gitEnvs,
                                    },
                                    CaptureSpecification.Sanitized,
                                    cancellationToken);
                            }
                        }
                    }
                }
            }

            // Process the submodules, only checking out submodules that sit underneath the target directory for compilation.
            _logger.LogInformation("Updating submodules...");
            await foreach (var iter in IterateContentBasedSubmodulesAsync(reservation, descriptor))
            {
                await CheckoutSubmoduleAsync(
                    git,
                    gitEnvs,
                    uri,
                    iter.contentDirectory,
                    iter.submodule,
                    iter.submoduleGitDirectory,
                    cancellationToken);
            }

            // Write our .gitcheckout file which tells subsequent calls that we're up-to-date.
            await File.WriteAllTextAsync(
                Path.Combine(reservation.ReservedPath, ".gitcheckout"),
                targetCommit);
        }
    }
}
