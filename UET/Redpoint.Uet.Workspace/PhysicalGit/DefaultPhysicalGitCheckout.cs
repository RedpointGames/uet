namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Redpoint.Concurrency;
    using Microsoft.Extensions.Logging;
    using Redpoint.CredentialDiscovery;
    using Redpoint.IO;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.Compression;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Principal;
    using System.Buffers.Text;

    internal class DefaultPhysicalGitCheckout : IPhysicalGitCheckout
    {
        private readonly ILogger<DefaultPhysicalGitCheckout> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IReservationManagerForUet _reservationManagerForUet;
        private readonly ICredentialDiscovery _credentialDiscovery;
        private readonly IReservationManagerFactory _reservationManagerFactory;
        private readonly IWorldPermissionApplier _worldPermissionApplier;
        private readonly ConcurrentDictionary<string, IReservationManager> _sharedReservationManagers;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;

        public DefaultPhysicalGitCheckout(
            ILogger<DefaultPhysicalGitCheckout> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IReservationManagerForUet reservationManagerForUet,
            ICredentialDiscovery credentialDiscovery,
            IReservationManagerFactory reservationManagerFactory,
            IWorldPermissionApplier worldPermissionApplier)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _reservationManagerForUet = reservationManagerForUet;
            _credentialDiscovery = credentialDiscovery;
            _reservationManagerFactory = reservationManagerFactory;
            _worldPermissionApplier = worldPermissionApplier;
            _sharedReservationManagers = new ConcurrentDictionary<string, IReservationManager>();
            _globalMutexReservationManager = _reservationManagerFactory.CreateGlobalMutexReservationManager();
        }

        /// <summary>
        /// Upgrades the system-wide version of Git if necessary.
        /// </summary>
        private async Task UpgradeSystemWideGitIfPossibleAsync()
        {
            if (OperatingSystem.IsWindows())
            {
                // Make sure we're an Administrator.
                var isAdministrator = false;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    isAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                if (!isAdministrator)
                {
                    _logger.LogInformation("Skipping automatic upgrade/install of Git because this process is not running as an Administrator.");
                    return;
                }

                // Try to find PowerShell 7 via PATH. The WinGet CLI doesn't work under SYSTEM (even with absolute path) due to MSIX nonsense, but apparently the PowerShell scripts use a COM API that does?
                string? pwsh = null;
                try
                {
                    pwsh = await _pathResolver.ResolveBinaryPath("pwsh").ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                }
                if (pwsh == null)
                {
                    _logger.LogInformation("Skipping automatic upgrade/install of Git because this system does not have PowerShell 7 or later installed.");
                    return;
                }

                // If Chocolatey is installed, remove any version of Git that Chocolatey has previously installed because we'll manage it with WinGet from here on out.
                string? choco = null;
                try
                {
                    choco = await _pathResolver.ResolveBinaryPath("choco").ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                }
                if (choco != null)
                {
                    var packagesToRemove = new List<LogicalProcessArgument>();
                    if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "lib", "git")))
                    {
                        packagesToRemove.Add("git");
                    }
                    if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "lib", "git.install")))
                    {
                        packagesToRemove.Add("git.install");
                    }
                    if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "lib", "git.portable")))
                    {
                        packagesToRemove.Add("git.portable");
                    }

                    if (packagesToRemove.Count > 0)
                    {
                        _logger.LogInformation("Removing any version of Git that is managed by Chocolatey...");
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = choco,
                                Arguments = new LogicalProcessArgument[]
                                {
                                    "uninstall",
                                    "-r",
                                    "-y",
                                }.Concat(packagesToRemove)
                            },
                            CaptureSpecification.Passthrough,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }

                // Make sure Git is up-to-date.
                await using (await _globalMutexReservationManager.TryReserveExactAsync("GitUpgrade").ConfigureAwait(false))
                {
                    _logger.LogInformation("Ensuring Git is up-to-date...");
                    var script =
                        """
                        if ($null -eq (Get-InstalledModule -ErrorAction SilentlyContinue -Name Microsoft.WinGet.Client)) {
                            Write-Host "Installing WinGet PowerShell module because it's not currently installed...";
                            Install-Module -Name Microsoft.WinGet.Client -Force;
                        }
                        $InstalledPackage = (Get-WinGetPackage -Id Microsoft.Git -ErrorAction SilentlyContinue);
                        if ($null -eq $InstalledPackage) {
                            Write-Host "Installing Git because it's not currently installed...";
                            Install-WinGetPackage -Id Microsoft.Git -Mode Silent;
                            exit 0;
                        } else if ($InstalledPackage.Version -ne (Find-WinGetPackage -Id Microsoft.Git).Version) {
                            Write-Host "Updating Git because it's not the latest version...";
                            Update-WinGetPackage -Id Microsoft.Git -Mode Silent;
                            exit 0;
                        }
                        """;
                    var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = pwsh,
                            Arguments = [
                                "-NonInteractive",
                                "-OutputFormat",
                                "Text",
                                "-EncodedCommand",
                                encodedScript,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                // Make sure Homebrew is installed so we can automate install/upgrade of Git.
                string? brew = null;
                try
                {
                    brew = await _pathResolver.ResolveBinaryPath("brew").ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                }
                if (brew == null)
                {
                    _logger.LogInformation("Skipping automatic upgrade/install of Git because Homebrew is not installed.");
                    return;
                }

                // Make sure Git is up-to-date.
                await using (await _globalMutexReservationManager.TryReserveExactAsync("GitUpgrade").ConfigureAwait(false))
                {
                    _logger.LogInformation("Ensuring Git is up-to-date...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = brew,
                            Arguments = [
                                "upgrade",
                                File.Exists("/opt/homebrew/bin/git") ? "upgrade" : "install",
                                "git",
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Ensures that the directory we potentially installed Git to in UpgradeSystemWideGitIfPossibleAsync is on the process's current PATH variable.
        /// </summary>
        private Task EnsureGitIsOnProcessPATH()
        {
            if (OperatingSystem.IsWindows())
            {
                var targetPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Git",
                    "cmd");
                if (File.Exists(Path.Combine(targetPath, "git.exe")))
                {
                    var path = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';').ToList();
                    if (!path.Contains(targetPath))
                    {
                        _logger.LogInformation($"Adding '{targetPath}' to process PATH as it is not currently present.");
                        path.Insert(0, targetPath);
                        Environment.SetEnvironmentVariable("PATH", string.Join(';', path));
                    }
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var targetPath = "/opt/homebrew/bin";
                if (File.Exists(Path.Combine(targetPath, "git")))
                {
                    var path = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':').ToList();
                    if (!path.Contains(targetPath))
                    {
                        _logger.LogInformation($"Adding '{targetPath}' to process PATH as it is not currently present.");
                        path.Insert(0, targetPath);
                        Environment.SetEnvironmentVariable("PATH", string.Join(':', path));
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Ensures that Git is currently a new enough version for UET to use.
        /// </summary>
        private async Task EnsureGitIsNewEnoughVersionAsync()
        {
            // Ensure Git is installed at all.
            string? git = null;
            try
            {
                git = await _pathResolver.ResolveBinaryPath("git").ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
            }
            if (git == null)
            {
                throw new InvalidOperationException($"Git is not currently installed, and could not be installed by UET. Please install it before running UET on build servers, or ensure that UET is running with administrative privileges.");
            }

            // Run --version to get the full version string.
            var stdout = new StringBuilder();
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = ["--version"],
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);

            // Get the version number component.
            var versionNumber = new Regex(" ([0-9]+)\\.([0-9]+)\\.[0-9]").Match(stdout.ToString());
            if (versionNumber.Success && versionNumber.Groups.Count >= 3 &&
                int.TryParse(versionNumber.Groups[1].Value, out var major) &&
                int.TryParse(versionNumber.Groups[2].Value, out var minor))
            {
                if (major < 2 || (major == 2 && minor < 46))
                {
                    throw new InvalidOperationException($"This version of Git is too old. UET requires at least Git 2.46.0. Please upgrade Git on this machine, or ensure that UET is running with administrative privileges.");
                }
            }
            else
            {
                _logger.LogWarning($"Unable to determine Git version from version string '{stdout.ToString()}'.");
            }
        }

        /// <summary>
        /// Initializes an empty Git workspace in the target directory if one doesn't already exist.
        /// </summary>
        private async Task InitGitWorkspaceIfNeededAsync(
            string repositoryPath,
            GitWorkspaceDescriptor workspaceDescriptor,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;

            if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
            {
                _logger.LogInformation("Initializing Git repository because it doesn't already exist...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "init",
                            repositoryPath
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git init' exited with non-zero exit code {exitCode}");
                }

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "config",
                            "set",
                            "core.symlinks",
                            "true"
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... config set core.symlinks true' exited with non-zero exit code {exitCode}");
                }
            }

            if (workspaceDescriptor.LfsStoragePath != null)
            {
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "config",
                            "set",
                            "lfs.storage",
                            workspaceDescriptor.LfsStoragePath
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "config",
                            "unset",
                            "lfs.storage"
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Attempts to resolve a reference such as a branch name or tag to a commit hash, without hitting
        /// the network. The reference can not be resolved if it is a branch name or tag and does not exist
        /// locally.
        /// 
        /// This function is used to perform early "are we up to date?" checks prior to performing network
        /// operations.
        /// </summary>
        private async Task<GitPotentiallyResolvedReference> AttemptResolveReferenceToCommitWithoutFetchAsync(
            string repositoryPath,
            Uri repositoryUri,
            string targetReference,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;
            var targetCommit = targetReference;
            var targetIsPotentialAnnotatedTag = false;
            if (!new Regex("^[a-f0-9]{40}$").IsMatch(targetCommit))
            {
                _logger.LogInformation($"Resolving ref '{targetCommit}' to commit on remote Git server...");
                var resolvedRefStringBuilder = new StringBuilder();
                using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = gitContext.Git,
                            Arguments =
                            [
                                "ls-remote",
                                "--exit-code",
                                repositoryUri.ToString(),
                                targetCommit,
                            ],
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(resolvedRefStringBuilder),
                        cancellationToken).ConfigureAwait(false);
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
                foreach (var line in resolvedRefStringBuilder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                {
                    var component = line.Replace("\t", " ", StringComparison.Ordinal).Split(" ")[0];
                    if (line.Contains("refs/tags/", StringComparison.Ordinal))
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
            return new GitPotentiallyResolvedReference
            {
                TargetCommitOrUnresolvedReference = targetCommit,
                TargetIsPotentialAnnotatedTag = targetIsPotentialAnnotatedTag,
            };
        }

        /// <summary>
        /// Returns whether the last checkout operation put the directory at the specified reference.
        /// </summary>
        private async Task<bool> IsRepositoryUpToDateAsync(
            string repositoryPath,
            GitPotentiallyResolvedReference potentiallyResolvedReference,
            GitExecutionContext gitContext,
            GitWorkspaceDescriptor gitWorkspaceDescriptor,
            CancellationToken cancellationToken)
        {
            var currentHead = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = gitContext.Git,
                    Arguments =
                    [
                        "-C",
                        repositoryPath,
                        "rev-parse",
                        "HEAD"
                    ],
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitContext.GitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(currentHead),
                cancellationToken).ConfigureAwait(false);
            if (currentHead.ToString().Trim() == potentiallyResolvedReference.TargetCommitOrUnresolvedReference)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")))
                {
                    if (gitContext.EnableSubmoduleSupport)
                    {
                        // Just really quickly check to make sure the .git file exists in each submodule we care about. If it doesn't,
                        // then the .gitcheckout file is stale and needs to be removed.
                        var validSubmoduleLayout = true;
                        _logger.LogInformation("Quickly checking submodules...");
                        await foreach (var iter in IterateContentBasedSubmodulesAsync(repositoryPath, gitWorkspaceDescriptor))
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
                            return true;
                        }
                        else
                        {
                            File.Delete(Path.Combine(repositoryPath, ".gitcheckout"));
                            // Continue with full process...
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else if (File.Exists(Path.Combine(repositoryPath, ".gitcheckout")))
            {
                File.Delete(Path.Combine(repositoryPath, ".gitcheckout"));
                // Continue with full process...
            }
            return false;
        }

        /// <summary>
        /// Marks the repository as having completed a full checkout operation to the specified reference. We track
        /// checkout success seperately from Git, as we need to ensure submodules are correct as well.
        /// </summary>
        private static async Task MarkRepositoryAsUpToDateAsync(
            string repositoryPath,
            GitResolvedReference resolvedReference,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, ".gitcheckout"),
                resolvedReference.TargetCommit,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a reference such as a branch name or tag to a commit hash, this time fetching
        /// commits from the remote repository as needed to ensure that the reference can be resolved.
        /// </summary>
        private async Task<GitResolvedReference> ResolveReferenceToCommitWithPotentialFetchAsync(
            string repositoryPath,
            Uri uri,
            GitPotentiallyResolvedReference potentiallyResolvedReference,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;

            // Set up the initial commit - at this point targetCommit could be a SHA1, or it could be
            // a branch or tag name.
            var targetCommit = potentiallyResolvedReference.TargetCommitOrUnresolvedReference;

            // Check if we already have the target commit in history. If we do, skip fetch.
            var gitTypeBuilder = new StringBuilder();
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = gitContext.Git,
                    Arguments =
                    [
                        "-C",
                        repositoryPath,
                        "cat-file",
                        "-t",
                        targetCommit,
                    ],
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitContext.GitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                cancellationToken).ConfigureAwait(false);
            var gitType = gitTypeBuilder.ToString().Trim();

            // If we know this is an annotated commit, resolve which commit it points to.
            if (gitType == "tag")
            {
                var targetCommitBuilder = new StringBuilder();
                _ = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "rev-list",
                            "-n",
                            "1",
                            targetCommit,
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                    cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(targetCommitBuilder.ToString()))
                {
                    targetCommit = targetCommitBuilder.ToString().Trim();
                    gitTypeBuilder = new StringBuilder();
                    _ = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = gitContext.Git,
                            Arguments =
                            [
                                "-C",
                                repositoryPath,
                                "cat-file",
                                "-t",
                                targetCommit,
                            ],
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = gitContext.GitEnvs,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                        cancellationToken).ConfigureAwait(false);
                    gitType = gitTypeBuilder.ToString().Trim();
                }
            }

            // If we couldn't resolve the reference, we don't have the commit.
            var didFetch = false;
            if (gitType != "commit")
            {
                _logger.LogInformation("Fetching repository from remote server...");

                // Fetch the commit that we need.
                while (true)
                {
                    var fetchStringBuilder = new StringBuilder();
                    if (potentiallyResolvedReference.TargetIsPotentialAnnotatedTag)
                    {
                        using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
                        {
                            exitCode = await FaultTolerantGitAsync(
                                new ProcessSpecification
                                {
                                    FilePath = gitContext.Git,
                                    Arguments =
                                    [
                                        "-C",
                                        repositoryPath,
                                        "fetch",
                                        "-f",
                                        "--filter=tree:0",
                                        "--recurse-submodules=no",
                                        "--progress",
                                        uri.ToString(),
                                        targetCommit
                                    ],
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                },
                                CaptureSpecification.Sanitized,
                                cancellationToken).ConfigureAwait(false);
                        }
                        // Now that we've fetched the potential tag, check if it really is a tag. If it is, resolve it to the commit hash instead.
                        gitTypeBuilder = new StringBuilder();
                        _ = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = gitContext.Git,
                                Arguments =
                                [
                                    "-C",
                                    repositoryPath,
                                    "cat-file",
                                    "-t",
                                    targetCommit,
                                ],
                                WorkingDirectory = repositoryPath,
                                EnvironmentVariables = gitContext.GitEnvs,
                            },
                            CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeBuilder),
                            cancellationToken).ConfigureAwait(false);
                        gitType = gitTypeBuilder.ToString().Trim();
                        if (gitType == "tag")
                        {
                            var targetCommitBuilder = new StringBuilder();
                            _ = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = gitContext.Git,
                                    Arguments =
                                    [
                                        "-C",
                                        repositoryPath,
                                        "rev-list",
                                        "-n",
                                        "1",
                                        targetCommit,
                                    ],
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = gitContext.GitEnvs,
                                },
                                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(targetCommitBuilder),
                                cancellationToken).ConfigureAwait(false);
                            targetCommit = targetCommitBuilder.ToString().Trim();
                        }
                    }
                    else
                    {
                        using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
                        {
                            exitCode = await FaultTolerantGitAsync(
                                new ProcessSpecification
                                {
                                    FilePath = gitContext.Git,
                                    Arguments =
                                    [
                                        "-C",
                                        repositoryPath,
                                        "fetch",
                                        "-f",
                                        "--filter=tree:0",
                                        "--recurse-submodules=no",
                                        "--progress",
                                        uri.ToString(),
                                        $"{targetCommit}:FETCH_HEAD"
                                    ],
                                    WorkingDirectory = repositoryPath,
                                    EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                                },
                                CaptureSpecification.Sanitized,
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                    if (fetchStringBuilder.ToString().Contains("fatal: early EOF", StringComparison.Ordinal))
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

                // Indicate to the caller that they might need to fetch LFS objects.
                didFetch = true;
            }

            return new GitResolvedReference
            {
                TargetCommit = targetCommit,
                DidFetch = didFetch,
            };
        }

        /// <summary>
        /// Check if Git LFS is enabled for the target commit. If it is, disable the previous partial
        /// clone behaviour and fetch all the commit data from Git, before running a 'git lfs fetch'
        /// operation.
        /// 
        /// 'git lfs fetch' can not work with partial clones, but we want to use partial clones when 
        /// LFS is not enabled since partial clones are faster than Git LFS (and should be used to
        /// replace it in newer repositories).
        /// </summary>
        private async Task DetectGitLfsAndReconfigureIfNecessaryAsync(
            string repositoryPath,
            Uri repositoryUri,
            GitResolvedReference resolvedReference,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;

            // Do not do anything if this target should not have Git LFS handled.
            if (!gitContext.EnableLfsSupport)
            {
                throw new InvalidOperationException("DetectGitLfsAndReconfigureIfNecessaryAsync should not be called if EnableLfsSupport is false!");
            }

            // Check to see if the target commit has a ".gitattributes" file with LFS filters in it.
            _logger.LogInformation("Checking for .gitattributes with LFS filters in it...");
            var gitAttributesContent = new StringBuilder();
            using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
            {
                var backoff = 1000;
                var attempts = 0;
                do
                {
                    gitAttributesContent.Clear();
                    var gitAttributesError = new StringBuilder();
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = gitContext.Git,
                            Arguments =
                            [
                                "-C",
                                repositoryPath,
                                "show",
                                $"{resolvedReference.TargetCommit}:.gitattributes",
                            ],
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                        {
                            ReceiveStdout = (line) =>
                            {
                                gitAttributesContent.AppendLine(line);
                                return false;
                            },
                            ReceiveStderr = (line) =>
                            {
                                gitAttributesError.AppendLine(line);
                                return false;
                            },
                        }),
                        cancellationToken).ConfigureAwait(false);
                    if (exitCode == 128 &&
                        gitAttributesError.ToString().Contains($"path '.gitattributes' does not exist", StringComparison.OrdinalIgnoreCase))
                    {
                        // Git LFS can't be enabled, because there's no .gitattributes file.
                        return;
                    }
                    if (exitCode == 128 && attempts < 10)
                    {
                        // 'git fetch' returns exit code 128 when the remote host
                        // unexpectedly disconnects. We want to handle unreliable
                        // network connections by simply retrying the 'git fetch'
                        // operation.
                        _logger.LogWarning($"'git show' encountered a network error while fetching commits. Retrying the fetch operation in {backoff}ms...");
                        await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
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
                        _logger.LogError("Fault tolerant Git operation ran into exit code 128 over 10 attempts, permanently failing...");
                    }

                    // Break if we returned success, throw otherwise.
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"'git show' exited with non-zero exit code {exitCode}");
                    }
                    else
                    {
                        break;
                    }
                } while (true);
            }

            // Check to see if .gitattributes contains LFS.
            if (!gitAttributesContent.ToString().Contains("filter=lfs", StringComparison.OrdinalIgnoreCase))
            {
                // No LFS filters enabled.
                return;
            }

            // LFS is enabled. Turn off any previous '--filter=tree:0' by removing the remote from the config.
            _logger.LogInformation("LFS is enabled. Turning off '--filter=tree:0'...");
            _ = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = gitContext.Git,
                    Arguments =
                    [
                        "-C",
                        repositoryPath,
                        "remote",
                        "remove",
                        repositoryUri.ToString(),
                    ],
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitContext.GitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken).ConfigureAwait(false);

            // Fetch the target commit again, this time without --filter=tree:0, using --refetch to force download.
            _logger.LogInformation("LFS is enabled. Fetching full repository history without partial clone...");
            using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
            {
                exitCode = await FaultTolerantGitAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "fetch",
                            "-f",
                            "--recurse-submodules=no",
                            "--progress",
                            "--refetch",
                            repositoryUri.ToString(),
                            resolvedReference.TargetCommit,
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... fetch -f --recurse-submodules=no --progress ...' exited with non-zero exit code {exitCode}");
                }
            }

            // Fetch the LFS objects.
            _logger.LogInformation("Fetching LFS files from remote server...");
            using (var fetchEnvVars = gitContext.FetchEnvironmentVariablesFactory())
            {
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "lfs",
                            "fetch",
                            repositoryUri.ToString(),
                            resolvedReference.TargetCommit,
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -C ... lfs fetch ...' exited with non-zero exit code {exitCode}");
                }
            }
        }

        /// <summary>
        /// Checks out the target commit, getting the working directory up-to-date (except submodules).
        /// This will automatically retry 'git lfs fetch' if the first checkout operation fails.
        /// </summary>
        private async Task CheckoutTargetCommitAsync(
            string repositoryPath,
            Uri repositoryUri,
            GitResolvedReference resolvedReference,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;

            _logger.LogInformation($"Checking out target commit {resolvedReference.TargetCommit}...");
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = gitContext.Git,
                    Arguments =
                    [
                        "-C",
                        repositoryPath,
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "--progress",
                        "-f",
                        resolvedReference.TargetCommit,
                    ],
                    WorkingDirectory = repositoryPath,
                    EnvironmentVariables = gitContext.GitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0 && gitContext.EnableLfsSupport)
            {
                // Attempt to re-fetch LFS files, in case that was the error.
                await DetectGitLfsAndReconfigureIfNecessaryAsync(
                    repositoryPath,
                    repositoryUri,
                    resolvedReference,
                    gitContext,
                    cancellationToken).ConfigureAwait(false);

                // Re-attempt checkout...
                _logger.LogInformation($"Re-attempting check out of target commit {resolvedReference.TargetCommit}...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            repositoryPath,
                            "-c",
                            "advice.detachedHead=false",
                            "checkout",
                            "--progress",
                            "-f",
                            resolvedReference.TargetCommit,
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
            }
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git checkout ...' exited with non-zero exit code {exitCode}");
            }
        }

        /// <summary>
        /// For projects and plugins, removes untracked content out of 'Source', 'Config', 'Resources' and 'Content'
        /// and cleans nukes all other folders, with the expectation that BuildGraph will populate them.
        /// </summary>
        private async Task CleanBuildSensitiveDirectoriesForProjectsAndPluginsAsync(
            string repositoryPath,
            GitExecutionContext gitContext,
            CancellationToken cancellationToken)
        {
            int exitCode;

            _logger.LogInformation($"Cleaning build sensitive directories...");
            var sensitiveDirectories = new string[] { "Source", "Config", "Resources", "Content" };
            foreach (var cleanFile in GetPluginAndProjectFiles(new DirectoryInfo(repositoryPath)))
            {
                var baseDirectory = GetGitBaseDirectoryForPath(cleanFile);
                var relativeBasePathToCleanDir = Path.GetRelativePath(baseDirectory.FullName, cleanFile.DirectoryName!).Replace("\\", "/", StringComparison.Ordinal).Trim('/');
                var relativeReservedPathToBase = Path.GetRelativePath(repositoryPath, baseDirectory.FullName).Replace("\\", "/", StringComparison.Ordinal).Trim('/');
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gitContext.Git,
                        Arguments =
                        [
                            "-C",
                            baseDirectory.FullName,
                            "ls-files",
                            "--error-unmatch",
                            $"{relativeBasePathToCleanDir}/{cleanFile.Name}",
                        ],
                        WorkingDirectory = repositoryPath,
                        EnvironmentVariables = gitContext.GitEnvs,
                    },
                    CaptureSpecification.Silence,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    // This is not a tracked project/plugin, nuke the build sensitive folders directly and expect BuildGraph to either populate or download them.
                    foreach (var sensitiveDirectory in sensitiveDirectories)
                    {
                        var sensitivePath = Path.Combine(cleanFile.DirectoryName!, sensitiveDirectory);
                        if (Directory.Exists(sensitivePath))
                        {
                            _logger.LogInformation($"Nuking: ({relativeReservedPathToBase}) {relativeBasePathToCleanDir}/{sensitiveDirectory}");
                            await DirectoryAsync.DeleteAsync(sensitivePath, true).ConfigureAwait(false);
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
                                    FilePath = gitContext.Git,
                                    Arguments =
                                    [
                                        "-C",
                                        baseDirectory.FullName,
                                        "clean",
                                        "-xdff",
                                        $"{relativeBasePathToCleanDir}/{sensitiveDirectory}",
                                    ],
                                    WorkingDirectory = baseDirectory.FullName,
                                    EnvironmentVariables = gitContext.GitEnvs,
                                },
                                CaptureSpecification.Sanitized,
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        #region Submodule Handling

        private static async Task<IEnumerable<GitSubmoduleDescription>> ParseSubmodulesAsync(string path)
        {
            var results = new List<GitSubmoduleDescription>();
            var submoduleGroupName = string.Empty;
            var submodulePath = string.Empty;
            var submoduleUrl = string.Empty;
            var submoduleExcludeOnMac = string.Empty;
            var gitmodulesPath = Path.Combine(path, ".gitmodules");
            if (!File.Exists(gitmodulesPath))
            {
                return results;
            }
            foreach (var line in await File.ReadAllLinesAsync(gitmodulesPath).ConfigureAwait(false))
            {
                if (line.StartsWith("[submodule \"", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(submoduleGroupName) &&
                        !string.IsNullOrWhiteSpace(submodulePath) &&
                        !string.IsNullOrWhiteSpace(submoduleUrl))
                    {
                        results.Add(new GitSubmoduleDescription
                        {
                            Id = submoduleGroupName,
                            Path = submodulePath,
                            Url = submoduleUrl,
                            ExcludeOnMac = submoduleExcludeOnMac.Equals("true", StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    submoduleGroupName = line["[submodule \"".Length..];
                    submoduleGroupName = submoduleGroupName[..^1];
                    submodulePath = string.Empty;
                    submoduleUrl = string.Empty;
                    submoduleExcludeOnMac = string.Empty;
                }
                else if (line.Trim().StartsWith("path = ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submodulePath = line[(line.IndexOf("path = ", StringComparison.Ordinal) + "path = ".Length)..].Trim();
                }
                else if (line.Trim().StartsWith("url = ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submoduleUrl = line[(line.IndexOf("url = ", StringComparison.Ordinal) + "url = ".Length)..].Trim();
                }
                else if (line.Trim().StartsWith("exclude-on-mac = ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(submoduleGroupName))
                {
                    submoduleExcludeOnMac = line[(line.IndexOf("exclude-on-mac = ", StringComparison.Ordinal) + "exclude-on-mac = ".Length)..].Trim();
                }
            }
            if (!string.IsNullOrWhiteSpace(submoduleGroupName) &&
                !string.IsNullOrWhiteSpace(submodulePath) &&
                !string.IsNullOrWhiteSpace(submoduleUrl))
            {
                results.Add(new GitSubmoduleDescription
                {
                    Id = submoduleGroupName,
                    Path = submodulePath,
                    Url = submoduleUrl,
                    ExcludeOnMac = submoduleExcludeOnMac.Equals("true", StringComparison.OrdinalIgnoreCase),
                });
            }
            return results;
        }

        private static IEnumerable<FileInfo> GetPluginAndProjectFiles(DirectoryInfo directory)
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

        private static DirectoryInfo GetGitBaseDirectoryForPath(FileInfo cleanFile)
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

        private static async IAsyncEnumerable<(DirectoryInfo contentDirectory, GitSubmoduleDescription submodule, DirectoryInfo submoduleGitDirectory)> IterateContentBasedSubmodulesAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor)
        {
            foreach (var topLevelSubmodule in await ParseSubmodulesAsync(repositoryPath).ConfigureAwait(false))
            {
                if (topLevelSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                {
                    continue;
                }

                // At the top-level of the repository, clone submodules that fall into any of these categories:
                // - Have /Source/ somewhere in their path relative to the root of the repository
                // - Are located within a <repository root>/Plugins/ directory
                // - If we are building a project, start with <project folder>/Plugins/
                if (topLevelSubmodule.Path.Contains("/Source/", StringComparison.Ordinal) ||
                    topLevelSubmodule.Path.StartsWith("Plugins/", StringComparison.Ordinal) ||
                    (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/", StringComparison.Ordinal)))
                {
                    yield return (
                        new DirectoryInfo(repositoryPath),
                        topLevelSubmodule,
                        new DirectoryInfo($"{repositoryPath}/.git/modules/{topLevelSubmodule.Id}"));

                    // Underneath top-level submodules, clone child submodules if the top-level submodule falls into
                    // any of these categories:
                    // - If we are building a project, start with <project folder>/Plugins/
                    if (descriptor.ProjectFolderName != null &&
                        topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/", StringComparison.Ordinal))
                    {
                        foreach (var childSubmodule in await ParseSubmodulesAsync($"{repositoryPath}/{topLevelSubmodule.Path}").ConfigureAwait(false))
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
            GitSubmoduleDescription submodule,
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
                    await File.WriteAllTextAsync(submoduleGitIndicatorFile, relativeModulePath, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation($"Initializing submodule {submodule.Path}...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments =
                            [
                                "-C",
                                submoduleContentPath,
                                "init",
                            ],
                            WorkingDirectory = submoduleContentPath,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken).ConfigureAwait(false);
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
                        Arguments =
                        [
                            "-C",
                            contentDirectory.FullName,
                            "submodule",
                            "absorbgitdirs",
                            "--",
                            submodule.Path,
                        ],
                        WorkingDirectory = contentDirectory.FullName,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Sanitized,
                    cancellationToken).ConfigureAwait(false);
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
                    Arguments =
                    [
                        "-C",
                        contentDirectory.FullName,
                        "ls-tree",
                        "-l",
                        "HEAD",
                        submodule.Path,
                    ],
                    WorkingDirectory = contentDirectory.FullName,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(submoduleStatusStringBuilder),
                cancellationToken).ConfigureAwait(false);
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
                    if (i == 0 && string.IsNullOrEmpty(relativePathComponents[i]))
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
                    Arguments =
                    [
                        "rev-parse",
                        "HEAD",
                    ],
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(currentHeadStringBuilder),
                cancellationToken).ConfigureAwait(false);
            var submoduleGitCheckoutPath = Path.Combine(submoduleContentPath, ".gitcheckout");
            if (currentHeadStringBuilder.ToString().Trim() == submoduleCommit)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(submoduleGitCheckoutPath))
                {
                    var lastSubmoduleCommit = (await File.ReadAllTextAsync(submoduleGitCheckoutPath, cancellationToken).ConfigureAwait(false)).Trim();
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
                    Arguments =
                    [
                        "cat-file",
                        "-t",
                        submoduleCommit,
                    ],
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(gitTypeStringBuilder),
                cancellationToken).ConfigureAwait(false);
            var gitType = gitTypeStringBuilder.ToString().Trim();
            if (gitType != "commit")
            {
                // Fetch the commit that we need.
                _logger.LogInformation($"Fetching submodule {submodule.Path} from remote server...");
                var (uri, fetchEnvironmentVariablesFactory) = ComputeRepositoryUriAndCredentials(submoduleUrl);
                using (var fetchEnvVars = fetchEnvironmentVariablesFactory())
                {
                    exitCode = await FaultTolerantGitAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments =
                            [
                                "fetch",
                                "-f",
                                "--filter=tree:0",
                                "--recurse-submodules=no",
                                uri.ToString(),
                                $"{submoduleCommit}:FETCH_HEAD",
                            ],
                            WorkingDirectory = submoduleContentPath,
                            EnvironmentVariables = fetchEnvVars.EnvironmentVariables,
                        },
                        CaptureSpecification.Sanitized,
                        cancellationToken).ConfigureAwait(false);
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
                    Arguments =
                    [
                        "-c",
                        "advice.detachedHead=false",
                        "checkout",
                        "--progress",
                        "-f",
                        submoduleCommit,
                    ],
                    WorkingDirectory = submoduleContentPath,
                    EnvironmentVariables = gitEnvs,
                },
                CaptureSpecification.Sanitized,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git checkout' for {submodule.Path} exited with non-zero exit code {exitCode}");
            }

            // Write our .gitcheckout file which tells subsequent calls that we're up-to-date.
            await File.WriteAllTextAsync(submoduleGitCheckoutPath, submoduleCommit, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Fault-Tolerant Git Network Operations

        private async Task<int> FaultTolerantGitAsync(
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
                    cancellationToken).ConfigureAwait(false);
                if (exitCode == 128 && attempts < 10)
                {
                    // 'git fetch' returns exit code 128 when the remote host
                    // unexpectedly disconnects. We want to handle unreliable
                    // network connections by simply retrying the 'git fetch'
                    // operation.
                    _logger.LogWarning($"'git' encountered a network error while fetching commits. Retrying the fetch operation in {backoff}ms...");
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
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
                    _logger.LogError("Fault tolerant Git operation ran into exit code 128 over 10 attempts, permanently failing...");
                    return exitCode;
                }

                // Some other exit code, just return.
                return exitCode;
            } while (true);
        }

        #endregion

        /// <summary>
        /// Applies the additional folder layers on top of the Git repository.
        /// </summary>
        private async Task ExtractAdditionalLayersToEngineWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                // @todo: Support macOS so that macOS is consistent with platforms on Windows.
                return;
            }

            var foldersToLayer = new List<string>(descriptor.AdditionalFolderLayers);
            foreach (var consoleZip in descriptor.AdditionalFolderZips)
            {
                await using ((await _reservationManagerForUet.ReserveAsync(
                    "ConsoleZip",
                    consoleZip).ConfigureAwait(false)).AsAsyncDisposable(out var reservation).ConfigureAwait(false))
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
                        var robocopy = await _pathResolver.ResolveBinaryPath("robocopy").ConfigureAwait(false);
                        var exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = robocopy,
                                Arguments =
                                [
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
                                ],
                                WorkingDirectory = repositoryPath,
                            },
                            CaptureSpecification.Silence,
                            ct).ConfigureAwait(false);
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
                                await Task.Delay(delay * 1000, ct).ConfigureAwait(false);
                                continue;
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Robocopy '{folder}' -> '{repositoryPath}': Success in {stopwatch.Elapsed.TotalSeconds,0} secs with exit code {exitCode}.");
                            break;
                        }
                    }
                }).ConfigureAwait(false);
        }

        private async Task RunGitDependenciesWithSharedCacheAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            int exitCode;

            await using ((await GetSharedGitDependenciesPath(descriptor, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var gitDepsCache).ConfigureAwait(false))
            {
                var gitDependenciesRootPath = Path.Combine(repositoryPath, "Engine", "Binaries", "DotNET", "GitDependencies");
                var gitDependenciesPath = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => Path.Combine(gitDependenciesRootPath, "win-x64", "GitDependencies.exe"),
                    var v when v == OperatingSystem.IsMacOS() => Path.Combine(gitDependenciesRootPath, "osx-x64", "GitDependencies"),
                    var v when v == OperatingSystem.IsLinux() => Path.Combine(gitDependenciesRootPath, "linux-x64", "GitDependencies"),
                    _ => throw new PlatformNotSupportedException("GitDependencies is not supported on this platform!"),
                };
                if (File.Exists(gitDependenciesPath))
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = gitDependenciesPath,
                            Arguments = Array.Empty<LogicalProcessArgument>(),
                            WorkingDirectory = repositoryPath,
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "UE_GITDEPS_ARGS", $"--all --force --cache={gitDepsCache.ReservedPath}" },
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        throw new InvalidOperationException($"'GitDependencies --all --force ...' exited with non-zero exit code {exitCode}");
                    }
                }
            }
        }

        private IReservationManager GetSharedReservationManagerForPath(string sharedGitCachePath)
        {
            return _sharedReservationManagers.GetOrAdd(sharedGitCachePath, x => _reservationManagerFactory.CreateReservationManager(x));
        }

        private async Task<IReservation> GetSharedGitDependenciesPath(GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows() && descriptor.WindowsSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.WindowsSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("GitDeps", cancellationToken).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsMacOS() && descriptor.MacSharedGitCachePath != null)
            {
                var reservationManager = GetSharedReservationManagerForPath(descriptor.MacSharedGitCachePath);
                return await reservationManager.ReserveExactAsync("GitDeps", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await _reservationManagerForUet.ReserveExactAsync($"UnrealEngineGitDeps", cancellationToken).ConfigureAwait(false);
            }
        }

        private (Uri uri, Func<GitTemporaryEnvVarsForFetch> fetchEnvironmentVariables) ComputeRepositoryUriAndCredentials(string repositoryUrl)
        {
            // Parse the repository URL.
            var uri = new Uri(repositoryUrl);
            try
            {
                var uriCredential = _credentialDiscovery.GetGitCredential(repositoryUrl);
                if (uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(uriCredential.SshPrivateKeyAsPem))
                    {
                        return (uri, () => new GitTemporaryEnvVarsForFetch(uriCredential.SshPrivateKeyAsPem));
                    }

                    return (uri, () => new GitTemporaryEnvVarsForFetch());
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

                    return (uri, () => new GitTemporaryEnvVarsForFetch());
                }
            }
            catch (UnableToDiscoverCredentialException ex)
            {
                _logger.LogWarning($"Unable to infer credential for Git URL. Assuming the environment is correctly set up to fetch commits from this URL. The original error was: {ex.Message}");
                return (uri, () => new GitTemporaryEnvVarsForFetch());
            }
        }

        public async Task PrepareGitWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            // Automatically upgrade Git if needed.
            await UpgradeSystemWideGitIfPossibleAsync().ConfigureAwait(false);

            // Ensure Git is on our PATH (this fixes up the PATH variable if we just installed Git via UpgradeSystemWideGitIfPossibleAsync).
            await EnsureGitIsOnProcessPATH().ConfigureAwait(false);

            // Ensure Git is new enough.
            await EnsureGitIsNewEnoughVersionAsync().ConfigureAwait(false);

            // Find Git and set up environment variables.
            var git = await _pathResolver.ResolveBinaryPath("git").ConfigureAwait(false);
            var gitEnvs = new Dictionary<string, string>
            {
                { "GIT_ASK_YESNO", "false" },
            };

            // Compute the repository URI and environment variable factory.
            var (repositoryUri, fetchEnvironmentVariablesFactory) = ComputeRepositoryUriAndCredentials(descriptor.RepositoryUrl);

            // Create the context that we'll use to execute Git in all our downstream functions.
            var gitContext = new GitExecutionContext
            {
                Git = git,
                GitEnvs = gitEnvs,
                FetchEnvironmentVariablesFactory = fetchEnvironmentVariablesFactory,
                EnableLfsSupport = descriptor.BuildType != GitWorkspaceDescriptorBuildType.Engine,
                EnableSubmoduleSupport = descriptor.BuildType == GitWorkspaceDescriptorBuildType.Generic,
            };

            // Initialize the Git repository if needed.
            await InitGitWorkspaceIfNeededAsync(repositoryPath, descriptor, gitContext, cancellationToken).ConfigureAwait(false);

            // Resolve tags and refs if needed.
            var potentiallyResolvedReference = await AttemptResolveReferenceToCommitWithoutFetchAsync(
                repositoryPath,
                repositoryUri,
                descriptor.RepositoryCommitOrRef,
                gitContext,
                cancellationToken).ConfigureAwait(false);

            // Check if we've already got the commit checked out.
            if (await IsRepositoryUpToDateAsync(
                repositoryPath,
                potentiallyResolvedReference,
                gitContext,
                descriptor,
                cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Git repository already up-to-date.");
                return;
            }

            // Resolve the target commit to a real commit hash, fetching repository commits if needed.
            var resolvedReference = await ResolveReferenceToCommitWithPotentialFetchAsync(
                repositoryPath,
                repositoryUri,
                potentiallyResolvedReference,
                gitContext,
                cancellationToken).ConfigureAwait(false);

            // If we fetched, check to see if Git LFS is enabled for the target commit, and if so, turn
            // off filtering and re-fetch the repository.
            if (resolvedReference.DidFetch && gitContext.EnableLfsSupport)
            {
                await DetectGitLfsAndReconfigureIfNecessaryAsync(
                    repositoryPath,
                    repositoryUri,
                    resolvedReference,
                    gitContext,
                    cancellationToken).ConfigureAwait(false);
            }

            // Checkout the target commit.
            await CheckoutTargetCommitAsync(
                repositoryPath,
                repositoryUri,
                resolvedReference,
                gitContext,
                cancellationToken).ConfigureAwait(false);

            // Clean all Source, Config, Resources and Content folders so that we don't have stale files accidentally included in build steps.
            if (descriptor.BuildType == GitWorkspaceDescriptorBuildType.Generic)
            {
                await CleanBuildSensitiveDirectoriesForProjectsAndPluginsAsync(
                    repositoryPath,
                    gitContext,
                    cancellationToken).ConfigureAwait(false);
            }

            // Process the submodules, only checking out submodules that sit underneath the target directory for compilation.
            if (gitContext.EnableSubmoduleSupport)
            {
                _logger.LogInformation("Updating submodules...");
                await foreach (var iter in IterateContentBasedSubmodulesAsync(repositoryPath, descriptor))
                {
                    await CheckoutSubmoduleAsync(
                        git,
                        gitEnvs,
                        repositoryUri,
                        iter.contentDirectory,
                        iter.submodule,
                        iter.submoduleGitDirectory,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (descriptor.BuildType == GitWorkspaceDescriptorBuildType.Engine)
            {
                // Copy our additional folder layers on top.
                await ExtractAdditionalLayersToEngineWorkspaceAsync(
                    repositoryPath,
                    descriptor,
                    cancellationToken).ConfigureAwait(false);

                // Run GitDependencies.exe with a shared cache to set up all the Unreal binary files.
                await RunGitDependenciesWithSharedCacheAsync(
                    repositoryPath,
                    descriptor,
                    cancellationToken).ConfigureAwait(false);
            }

            // Write our .gitcheckout file which tells subsequent calls that we're up-to-date.
            await MarkRepositoryAsUpToDateAsync(
                repositoryPath,
                resolvedReference,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
