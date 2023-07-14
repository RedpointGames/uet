namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CredentialDiscovery;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.Compression;
    using System.Security.Policy;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultPhysicalGitCheckout : IPhysicalGitCheckout
    {
        private readonly ILogger<DefaultPhysicalGitCheckout> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IReservationManagerForUet _reservationManagerForUet;
        private readonly ICredentialDiscovery _credentialDiscovery;
        private readonly IReservationManagerFactory _reservationManagerFactory;
        private readonly ConcurrentDictionary<string, IReservationManager> _sharedReservationManagers;

        public DefaultPhysicalGitCheckout(
            ILogger<DefaultPhysicalGitCheckout> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IReservationManagerForUet reservationManagerForUet,
            ICredentialDiscovery credentialDiscovery,
            IReservationManagerFactory reservationManagerFactory)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _reservationManagerForUet = reservationManagerForUet;
            _credentialDiscovery = credentialDiscovery;
            _reservationManagerFactory = reservationManagerFactory;
            _sharedReservationManagers = new ConcurrentDictionary<string, IReservationManager>();
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
            string repositoryPath,
            GitWorkspaceDescriptor descriptor)
        {
            foreach (var topLevelSubmodule in await ParseSubmodulesAsync(repositoryPath))
            {
                if (topLevelSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                {
                    continue;
                }

                if (topLevelSubmodule.Path.Contains("/Source/") || (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/")))
                {
                    yield return (
                        new DirectoryInfo(repositoryPath),
                        topLevelSubmodule,
                        new DirectoryInfo($"{repositoryPath}/.git/modules/{topLevelSubmodule.Id}"));
                    if (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/"))
                    {
                        foreach (var childSubmodule in await ParseSubmodulesAsync($"{repositoryPath}/{topLevelSubmodule.Path}"))
                        {
                            if (childSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                            {
                                continue;
                            }
                            yield return (
                                new DirectoryInfo($"{repositoryPath}/{topLevelSubmodule.Path}"),
                                childSubmodule,
                                new DirectoryInfo($"{repositoryPath}/.git/modules/${topLevelSubmodule.Id}/modules/{childSubmodule.Id}"));
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
                var (uri, fetchEnvironmentVariablesFactory) = ComputeRepositoryUriAndCredentials(submoduleUrl);
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
                    exitCode = await FaultTolerantFetchAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "fetch",
                                "-f",
                                "--recurse-submodules=no",
                                uri.ToString(),
                                $"{submoduleCommit}:FETCH_HEAD",
                            },
                            WorkingDirectory = submoduleContentPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken);
                }
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

        private async Task PrepareNonEngineGitWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            var git = await _pathResolver.ResolveBinaryPath("git");
            var gitEnvs = new Dictionary<string, string>
            {
                { "GIT_ASK_YESNO", "false" },
            };
            var exitCode = 0;

            var (uri, fetchEnvironmentVariablesFactory) = ComputeRepositoryUriAndCredentials(descriptor.RepositoryUrl);

            // Initialize the Git repository if needed.
            if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
            {
                _logger.LogInformation("Initializing Git repository because it doesn't already exist...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "init",
                            repositoryPath
                        },
                        WorkingDirectory = repositoryPath,
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
                            repositoryPath,
                            "config",
                            "core.symlinks",
                            "true"
                        },
                        WorkingDirectory = repositoryPath,
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
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
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
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(resolvedRefStringBuilder),
                        cancellationToken);
                }
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
                        repositoryPath,
                        "rev-parse",
                        "HEAD"
                    },
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(currentHead),
                cancellationToken);
            if (currentHead.ToString().Trim() == targetCommit)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")))
                {
                    // Just really quickly check to make sure the .git file exists in each submodule we care about. If it doesn't,
                    // then the .gitcheckout file is stale and needs to be removed.
                    var validSubmoduleLayout = true;
                    _logger.LogInformation("Quickly checking submodules...");
                    await foreach (var iter in IterateContentBasedSubmodulesAsync(repositoryPath, descriptor))
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
                        File.Delete(Path.Combine(repositoryPath, ".gitcheckout"));
                        // Continue with full process...
                    }
                }
            }
            else if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")))
            {
                File.Delete(Path.Combine(repositoryPath, ".gitcheckout"));
                // Continue with full process...
            }

            // Check if we already have the target commit in history. If we do, skip fetch.
            var gitTypeBuilder = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-C",
                        repositoryPath,
                        "cat-file",
                        "-t",
                        targetCommit,
                    },
                    WorkingDirectory = repositoryPath,
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
                            repositoryPath,
                            "rev-list",
                            "-n",
                            "1",
                            targetCommit,
                        },
                        WorkingDirectory = repositoryPath,
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
                                repositoryPath,
                                "cat-file",
                                "-t",
                                targetCommit,
                            },
                            WorkingDirectory = repositoryPath,
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
                        using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                        {
                            exitCode = await FaultTolerantFetchAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = new[]
                                    {
                                        "-C",
                                        repositoryPath,
                                        "fetch",
                                        "-f",
                                        "--recurse-submodules=no",
                                        "--progress",
                                        uri.ToString(),
                                        targetCommit
                                    },
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                },
                                CaptureSpecification.Sanitized,
                                cancellationToken);
                        }
                        // Now that we've fetched the potential tag, check if it really is a tag. If it is, resolve it to the commit hash instead.
                        gitTypeBuilder = new StringBuilder();
                        _ = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = new[]
                                {
                                    "-C",
                                    repositoryPath,
                                    "cat-file",
                                    "-t",
                                    targetCommit,
                                },
                                WorkingDirectory = repositoryPath,
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
                                        repositoryPath,
                                        "rev-list",
                                        "-n",
                                        "1",
                                        targetCommit,
                                    },
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = gitEnvs,
                                },
                                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                                cancellationToken);
                            targetCommit = targetCommitBuilder.ToString().Trim();
                        }
                    }
                    else
                    {
                        using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                        {
                            exitCode = await FaultTolerantFetchAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = new[]
                                    {
                                        "-C",
                                        repositoryPath,
                                        "fetch",
                                        "-f",
                                        "--recurse-submodules=no",
                                        "--progress",
                                        uri.ToString(),
                                        $"{targetCommit}:FETCH_HEAD"
                                    },
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                },
                                CaptureSpecification.Sanitized,
                                cancellationToken);
                        }
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
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "-C",
                                repositoryPath,
                                "lfs",
                                "fetch",
                                uri.ToString(),
                                targetCommit,
                            },
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken);
                }
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
                        repositoryPath,
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "-f",
                        targetCommit,
                    },
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken);
            if (exitCode != 0)
            {
                // Attempt to re-fetch LFS files, in case that was the error.
                _logger.LogInformation("Fetching LFS files from remote server...");
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "-C",
                                repositoryPath,
                                "lfs",
                                "fetch",
                                uri.ToString(),
                                targetCommit,
                            },
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken);
                }
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
                            repositoryPath,
                            "-c",
                            "advice.detachedHead=false",
                            "checkout",
                            "-f",
                            targetCommit,
                        },
                        WorkingDirectory = repositoryPath,
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
                foreach (var cleanFile in GetPluginAndProjectFiles(new DirectoryInfo(repositoryPath)))
                {
                    var baseDirectory = GetGitBaseDirectoryForPath(cleanFile);
                    var relativeBasePathToCleanDir = Path.GetRelativePath(baseDirectory.FullName, cleanFile.DirectoryName!).Replace("\\", "/").Trim('/');
                    var relativeReservedPathToBase = Path.GetRelativePath(repositoryPath, baseDirectory.FullName).Replace("\\", "/").Trim('/');
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
                            WorkingDirectory = repositoryPath,
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
            await foreach (var iter in IterateContentBasedSubmodulesAsync(repositoryPath, descriptor))
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
                Path.Combine(repositoryPath, ".gitcheckout"),
                targetCommit);
        }

        private async Task PrepareEngineGitWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            var git = await _pathResolver.ResolveBinaryPath("git");
            var gitEnvs = new Dictionary<string, string>
            {
                { "GIT_ASK_YESNO", "false" },
            };
            var exitCode = 0;

            var (uri, fetchEnvironmentVariablesFactory) = ComputeRepositoryUriAndCredentials(descriptor.RepositoryUrl);

            // Resolve tags and refs if needed.
            var targetCommit = descriptor.RepositoryCommitOrRef;
            var targetIsPotentialAnnotatedTag = false;
            if (!new Regex("^[a-f0-9]{40}$").IsMatch(targetCommit))
            {
                _logger.LogInformation($"Resolving ref '{targetCommit}' to commit on remote Git server...");
                var resolvedRefStringBuilder = new StringBuilder();
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
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
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(resolvedRefStringBuilder),
                        cancellationToken);
                }
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

            // We have our own .gitcheckout file which we write after we finish all work. That way, we know the previous checkout completed successfully even if it failed after doing 'git checkout'.
            if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")) && File.ReadAllText(Path.Combine(repositoryPath, ".gitcheckout")).Trim() == targetCommit)
            {
                _logger.LogInformation("Git repository already up-to-date.");
                return;
            }
            else if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")))
            {
                File.Delete(Path.Combine(repositoryPath, ".gitcheckout"));
                // Continue with full process...
            }
            var targetCommitForCommitStamp = targetCommit;

            // Because engines are very large, we want to clone/fetch into a single reservation for the
            // engine and then checkout the branch we need.
            string sharedBareRepoPath;
            await using (var sharedBareRepo = await GetSharedGitRepoPath(descriptor, cancellationToken))
            {
                sharedBareRepoPath = sharedBareRepo.ReservedPath;

                // Initialize the Git repository if needed.
                if (!Directory.Exists(Path.Combine(sharedBareRepo.ReservedPath, "objects")))
                {
                    _logger.LogInformation("Initializing shared Git repository because it doesn't already exist...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = new[]
                            {
                                "init",
                                "--bare",
                                sharedBareRepo.ReservedPath
                            },
                            WorkingDirectory = sharedBareRepo.ReservedPath,
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
                                sharedBareRepo.ReservedPath,
                                "config",
                                "core.symlinks",
                                "true"
                            },
                            WorkingDirectory = sharedBareRepo.ReservedPath,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"'git -C ... config core.symlinks true' exited with non-zero exit code {exitCode}");
                    }
                }

                // Check if we already have the target commit in history. If we do, skip fetch.
                var gitTypeBuilder = new StringBuilder();
                _ = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = new[]
                        {
                            "-C",
                            sharedBareRepo.ReservedPath,
                            "cat-file",
                            "-t",
                            targetCommit,
                        },
                        WorkingDirectory = sharedBareRepo.ReservedPath,
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
                                sharedBareRepo.ReservedPath,
                                "rev-list",
                                "-n",
                                "1",
                                targetCommit,
                            },
                            WorkingDirectory = sharedBareRepo.ReservedPath,
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
                                    sharedBareRepo.ReservedPath,
                                    "cat-file",
                                    "-t",
                                    targetCommit,
                                },
                                WorkingDirectory = sharedBareRepo.ReservedPath,
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
                            using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                            {
                                exitCode = await FaultTolerantFetchAsync(
                                    new ProcessSpecification
                                    {
                                        FilePath = git,
                                        Arguments = new[]
                                        {
                                            "-C",
                                            sharedBareRepo.ReservedPath,
                                            "fetch",
                                            "-f",
                                            "--recurse-submodules=no",
                                            "--progress",
                                            uri.ToString(),
                                            targetCommit
                                        },
                                        WorkingDirectory = sharedBareRepo.ReservedPath,
                                        EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                    },
                                    CaptureSpecification.Sanitized,
                                    cancellationToken);
                            }
                            // Now that we've fetched the potential tag, check if it really is a tag. If it is, resolve it to the commit hash instead.
                            gitTypeBuilder = new StringBuilder();
                            _ = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = new[]
                                    {
                                        "-C",
                                        sharedBareRepo.ReservedPath,
                                        "cat-file",
                                        "-t",
                                        targetCommit,
                                    },
                                    WorkingDirectory = sharedBareRepo.ReservedPath,
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
                                            sharedBareRepo.ReservedPath,
                                            "rev-list",
                                            "-n",
                                            "1",
                                            targetCommit,
                                        },
                                        WorkingDirectory = sharedBareRepo.ReservedPath,
                                        EnvironmentVariables = gitEnvs,
                                    },
                                    CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                                    cancellationToken);
                                targetCommit = targetCommitBuilder.ToString().Trim();
                            }
                        }
                        else
                        {
                            using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                            {
                                exitCode = await FaultTolerantFetchAsync(
                                    new ProcessSpecification
                                    {
                                        FilePath = git,
                                        Arguments = new[]
                                        {
                                            "-C",
                                            sharedBareRepo.ReservedPath,
                                            "fetch",
                                            "-f",
                                            "--recurse-submodules=no",
                                            "--progress",
                                            uri.ToString(),
                                            $"{targetCommit}:FETCH_HEAD"
                                        },
                                        WorkingDirectory = sharedBareRepo.ReservedPath,
                                        EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                    },
                                    CaptureSpecification.Sanitized,
                                    cancellationToken);
                            }
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
                        $"--git-dir={sharedBareRepoPath}",
                        $"--work-tree={repositoryPath}",
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "--progress",
                        "-f",
                        targetCommit,
                    },
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git checkout ...' exited with non-zero exit code {exitCode}");
            }

            // Copy our additional folder layers on top.
            if (OperatingSystem.IsWindows())
            {
                var foldersToLayer = new List<string>(descriptor.AdditionalFolderLayers);
                foreach (var consoleZip in descriptor.AdditionalFolderZips)
                {
                    await using (var reservation = await _reservationManagerForUet.ReserveAsync(
                        "ConsoleZip",
                        consoleZip))
                    {
                        var extractPath = Path.Combine(reservation.ReservedPath, "extracted");
                        Directory.CreateDirectory(extractPath);
                        if (!File.Exists(Path.Combine(reservation.ReservedPath, ".console-zip-extracted")))
                        {
                            _logger.LogInformation($"Extracting '{consoleZip}' to '{extractPath}'...");
                            using (var stream = new FileStream(consoleZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                var archive = new ZipArchive(stream);
                                archive.ExtractToDirectory(extractPath);
                            }
                            File.WriteAllText(Path.Combine(reservation.ReservedPath, ".console-zip-extracted"), "done");
                        }
                        foldersToLayer.Add(extractPath);
                    }
                }
                await Parallel.ForEachAsync(
                    foldersToLayer.ToAsyncEnumerable(),
                    cancellationToken,
                    async (folder, ct) =>
                    {
                        var maxAttempts = 5;
                        for (var attempt = 0; attempt < maxAttempts; attempt++)
                        {
                            _logger.LogInformation($"Robocopy '{folder}' -> '{repositoryPath}': Started...");
                            var stopwatch = Stopwatch.StartNew();
                            var robocopy = await _pathResolver.ResolveBinaryPath("robocopy");
                            var exitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = robocopy,
                                    Arguments = new[]
                                    {
                                        folder,
                                        repositoryPath,
                                        "/E",
                                        "/NS",
                                        "/NC",
                                        "/NFL",
                                        "/NDL",
                                        "/NP",
                                        "/NJH",
                                        "/NJS"
                                    },
                                    WorkingDirectory = repositoryPath,
                                },
                                CaptureSpecification.Silence,
                                ct);
                            stopwatch.Stop();
                            if (exitCode > 8)
                            {
                                if (attempt == maxAttempts - 1)
                                {
                                    _logger.LogError($"Robocopy '{folder}' -> '{repositoryPath}': Failed in {stopwatch.Elapsed.TotalSeconds,0} secs with exit code {exitCode}.");
                                    throw new InvalidOperationException("Failed to copy folders via robocopy!");
                                }
                                else
                                {
                                    var delay = (attempt * 10);
                                    _logger.LogWarning($"Robocopy '{folder}' -> '{repositoryPath}': Failed in {stopwatch.Elapsed.TotalSeconds,0} secs with exit code {exitCode}, retrying in {delay} seconds...");
                                    await Task.Delay(delay * 1000);
                                    continue;
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"Robocopy '{folder}' -> '{repositoryPath}': Success in {stopwatch.Elapsed.TotalSeconds,0} secs with exit code {exitCode}.");
                                break;
                            }
                        }
                    });
            }

            // First run GitDependencies.exe to fetch all our binary dependencies into a shared cache.
            await using (var gitDepsCache = await GetSharedGitDependenciesPath(descriptor, cancellationToken))
            {
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = OperatingSystem.IsWindows()
                            ? Path.Combine(repositoryPath, "Engine", "Binaries", "DotNET", "GitDependencies", "win-x64", "GitDependencies.exe")
                            : Path.Combine(repositoryPath, "Engine", "Binaries", "DotNET", "osx-x64", "GitDependencies"),
                        Arguments = Array.Empty<string>(),
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = new Dictionary<string, string>
                        {
                            { "UE_GITDEPS_ARGS", $"--all --force --cache={gitDepsCache.ReservedPath}" },
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'GitDependencies --all --force ...' exited with non-zero exit code {exitCode}");
                }
            }

            // Write our .gitcheckout file which tells subsequent calls that we're up-to-date.
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, ".gitcheckout"),
                targetCommitForCommitStamp);
        }

        private IReservationManager GetSharedReservationManagerForPath(string sharedGitCachePath)
        {
            return _sharedReservationManagers.GetOrAdd(sharedGitCachePath, x => _reservationManagerFactory.CreateReservationManager(x));
        }

        private async Task<IReservation> GetSharedGitRepoPath(GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows() && descriptor.WindowsSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.WindowsSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("Git", cancellationToken);
            }
            else if (OperatingSystem.IsMacOS() && descriptor.MacSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.MacSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("Git", cancellationToken);
            }
            else
            {
                return await _reservationManagerForUet.ReserveExactAsync($"UnrealEngineGit", cancellationToken);
            }
        }

        private async Task<IReservation> GetSharedGitDependenciesPath(GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows() && descriptor.WindowsSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.WindowsSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("GitDeps", cancellationToken);
            }
            else if (OperatingSystem.IsMacOS() && descriptor.MacSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.MacSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("GitDeps", cancellationToken);
            }
            else
            {
                return await _reservationManagerForUet.ReserveExactAsync($"UnrealEngineGitDeps", cancellationToken);
            }
        }

        private class TemporaryEnvVarsForFetch : IDisposable
        {
            private readonly string? _path;
            private readonly Dictionary<string, string>? _envVars;

            public TemporaryEnvVarsForFetch()
            {
                _path = null;
                _envVars = new Dictionary<string, string>
                {
                    { "GIT_ASK_YESNO", "false" },
                };
            }

            public TemporaryEnvVarsForFetch(string privateKey)
            {
                _path = Path.GetTempFileName();
                using (var stream = new StreamWriter(new FileStream(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.None)))
                {
                    // @note: Private key content *must* have a newline at the end.
                    stream.Write(privateKey.Replace("\r\n", "\n").Trim() + "\n");
                }

                // @note: The identity file path format is extremely jank.
                var identityPath = _path;
                if (OperatingSystem.IsWindows())
                {
                    var root = Path.GetPathRoot(identityPath)!;
                    root = $"/{root[0].ToString().ToLowerInvariant()}";
                    identityPath = identityPath.Substring(root.Length);
                    identityPath = root + "/" + identityPath.Replace("\\", "/").TrimStart('/');
                }
                identityPath = identityPath.Replace(" ", "\\ ");

                _envVars = new Dictionary<string, string>
                {
                    { "GIT_SSH_COMMAND", $@"ssh -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -i {identityPath}" },
                    { "GIT_ASK_YESNO", "false" },
                };
            }

            public Dictionary<string, string>? EnvironmentVariables => _envVars;

            public void Dispose()
            {
                if (_path != null)
                {
                    File.Delete(_path);
                }
            }
        }

        private (Uri uri, Func<TemporaryEnvVarsForFetch> fetchEnvironmentVariables) ComputeRepositoryUriAndCredentials(string repositoryUrl)
        {
            // Parse the repository URL.
            var uri = new Uri(repositoryUrl);
            try
            {
                var uriCredential = _credentialDiscovery.GetGitCredential(repositoryUrl);
                if (uri.Scheme.Equals("ssh", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(uriCredential.SshPrivateKeyAsPem))
                    {
                        return (uri, () => new TemporaryEnvVarsForFetch(uriCredential.SshPrivateKeyAsPem));
                    }

                    return (uri, () => new TemporaryEnvVarsForFetch());
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(uriCredential.Password))
                    {
                        var builder = new UriBuilder(uri);
                        builder.UserName = uriCredential.Username;
                        builder.Password = uriCredential.Password;
                        uri = builder.Uri;
                    }

                    return (uri, () => new TemporaryEnvVarsForFetch());
                }
            }
            catch (UnableToDiscoverCredentialException ex)
            {
                _logger.LogWarning($"Unable to infer credential for Git URL. Assuming the environment is correctly set up to fetch commits from this URL. The original error was: {ex.Message}");
                return (uri, () => new TemporaryEnvVarsForFetch());
            }
        }

        public Task PrepareGitWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            if (!descriptor.IsEngineBuild)
            {
                return PrepareNonEngineGitWorkspaceAsync(
                    repositoryPath,
                    descriptor,
                    cancellationToken);
            }
            else
            {
                return PrepareEngineGitWorkspaceAsync(
                    repositoryPath,
                    descriptor,
                    cancellationToken);
            }
        }
    }
}
