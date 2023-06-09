namespace Redpoint.UET.Workspace.PhysicalGit
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.UET.Workspace.Descriptors;
    using System;
    using System.Collections.Generic;
    using System.Text;
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

        public async Task PrepareGitWorkspaceAsync(
            IReservation reservation,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            var git = await _pathResolver.ResolveBinaryPath("git");
            var exitCode = 0;

            // Initialize the Git repository if needed.
            if (!Directory.Exists(Path.Combine(reservation.ReservedPath, ".git")))
            {
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
                    },
                    CaptureSpecification.Passthrough,
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
                            "-c",
                            reservation.ReservedPath,
                            "config",
                            "core.symlinks",
                            "true"
                        },
                        WorkingDirectory = reservation.ReservedPath,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"'git -c ... config core.symlinks true' exited with non-zero exit code {exitCode}");
                }
            }

            // Check if we've already got the commit checked out.
            var currentHead = new StringBuilder();
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = git,
                    Arguments = new[]
                    {
                        "-c",
                        reservation.ReservedPath,
                        "rev-parse",
                        "HEAD"
                    },
                    WorkingDirectory = reservation.ReservedPath,
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(currentHead),
                cancellationToken);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"'git -c ... config core.symlinks true' exited with non-zero exit code {exitCode}");
            }
            if (currentHead.ToString().Trim() == descriptor.RepositoryCommit)
            {
                // We have our own .gitcheckout file which we write after we finish all submodule work. That way, we know the previous checkout completed successfully even if it failed during submodule work.
                if (File.Exists(Path.Combine(reservation.ReservedPath, ".gitcheckout")))
                {
                    // Just really quickly check to make sure the .git file exists in each submodule we care about. If it doesn't,
                    // then the .gitcheckout file is stale and needs to be removed.
                    var validSubmoduleLayout = true;
                    foreach (var topLevelSubmodule in await ParseSubmodulesAsync(reservation.ReservedPath))
                    {
                        if (topLevelSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                        {
                            continue;
                        }

                        if (topLevelSubmodule.Path.Contains("/Source/") || (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/")))
                        {
                            var dotGitPath = Path.Combine(reservation.ReservedPath, topLevelSubmodule.Path, ".git");
                            if (!File.Exists(dotGitPath))
                            {
                                _logger.LogWarning($"Missing .git file at {dotGitPath}, purging .gitcheckout...");
                                validSubmoduleLayout = false;
                                break;
                            }
                            if (descriptor.ProjectFolderName != null && topLevelSubmodule.Path.StartsWith($"{descriptor.ProjectFolderName}/Plugins/"))
                            {
                                foreach (var childSubmodule in await ParseSubmodulesAsync(Path.Combine(reservation.ReservedPath, topLevelSubmodule.Path)))
                                {
                                    if (topLevelSubmodule.ExcludeOnMac && OperatingSystem.IsMacOS())
                                    {
                                        continue;
                                    }

                                    if (childSubmodule.Path.Contains("/Source/"))
                                    {
                                        var childDotGitPath = Path.Combine(reservation.ReservedPath, topLevelSubmodule.Path, childSubmodule.Path, ".git");
                                        if (!File.Exists(childDotGitPath))
                                        {
                                            _logger.LogWarning($"Missing .git file at {dotGitPath}, purging .gitcheckout...");
                                            validSubmoduleLayout = false;
                                            break;
                                        }
                                    }
                                }
                                if (!validSubmoduleLayout)
                                {
                                    break;
                                }
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
            }

            // @note: For each folder layer, copy it over and then add it to .git/ignore.
            // @note: We probably need to check .git/ignore and remove any layers that
            // were added previously? Or make folder layers part of the reservation parameters...
        }
    }
}
