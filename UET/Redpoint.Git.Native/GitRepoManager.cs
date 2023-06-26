namespace Redpoint.Git.Native
{
    using LibGit2Sharp;
    using LibGit2Sharp.Handlers;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Security.Principal;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class GitRepoManager : IGitRepoManager
    {
        private readonly string _gitRepoPath;
        private readonly Repository _repository;
        private readonly ILogger<GitRepoManager> _logger;
        private readonly List<Process> _processes;
        private readonly SemaphoreSlim _processSemaphore;

        [SupportedOSPlatform("macos"), SupportedOSPlatform("linux")]
        [DllImport("libc")]
        private static extern int chown(string path, uint owner, uint group);

        [SupportedOSPlatform("macos"), SupportedOSPlatform("linux")]
        [DllImport("libc")]
        internal static extern uint geteuid();

        [SupportedOSPlatform("macos"), SupportedOSPlatform("linux")]
        [DllImport("libc")]
        internal static extern uint getegid();

        public GitRepoManager(
            ILogger<GitRepoManager> logger,
            string gitRepoPath)
        {
            _gitRepoPath = gitRepoPath;
            Directory.CreateDirectory(_gitRepoPath);

            if (OperatingSystem.IsWindows())
            {
                logger.LogInformation($"Setting {_gitRepoPath} to be accessible by the current user...");
                var directoryInfo = new DirectoryInfo(_gitRepoPath);
                var fs = directoryInfo.GetAccessControl();
                var user = WindowsIdentity.GetCurrent().User;
                if (user != null)
                {
                    fs.SetOwner(user);
                    directoryInfo.SetAccessControl(fs);
                }
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                logger.LogInformation($"Changing {_gitRepoPath} to be owned by the current user...");
                chown(_gitRepoPath, geteuid(), getegid());
            }

            var gitGlobalPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gitconfig");
            if (!File.Exists(gitGlobalPath))
            {
                logger.LogInformation($"Creating global Git configuration file at {gitGlobalPath}...");
                File.WriteAllText(gitGlobalPath, string.Empty);
            }

            logger.LogInformation($"Loading the Git configuration...");
            var configuration = Configuration.BuildFrom(null);
            if (!(configuration.Get<string>("safe.directory")?.Value?.Contains(_gitRepoPath.Replace("\\", "\\\\")) ?? false))
            {
                logger.LogInformation($"Adding {_gitRepoPath} to safe.directory in the global Git configuration file.");
                configuration.Add("safe.directory", _gitRepoPath.Replace("\\", "\\\\"), ConfigurationLevel.Global);
            }

            try
            {
                _repository = new Repository(_gitRepoPath);
                logger.LogInformation($"Loaded existing repository located at: {_gitRepoPath}");
            }
            catch (RepositoryNotFoundException)
            {
                Repository.Init(_gitRepoPath, true);
                _repository = new Repository(_gitRepoPath);
                logger.LogInformation($"Initialized new repository located at: {_gitRepoPath}");
            }

            _logger = logger;
            _processes = new List<Process>();
            _processSemaphore = new SemaphoreSlim(1);
        }

        public bool HasCommit(
            string commit)
        {
            _logger.LogInformation($"Checking if commit {commit} exists in repository...");
            if (GitResolver.ResolveToCommitHash(_repository, commit) != null)
            {
                _logger.LogInformation($"{commit} exists in repository.");
                return true;
            }
            else
            {
                _logger.LogInformation($"{commit} does not exist in repository.");
                return false;
            }
        }

        class DataHolder
        {
            public long BytesReceived;
        }

        public async Task Fetch(
            string url,
            string commit,
            string publicKeyFile,
            string privateKeyFile,
            Action<GitFetchProgressInfo> onProgress)
        {
            await Task.Run(() =>
            {
                string hash;
                using (var sha = SHA1.Create())
                {
                    hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).ToLowerInvariant().Replace("-", "");
                }

                var remoteName = $"remote-{hash.Substring(0, 8)}";
                if (_repository.Network.Remotes.Any(x => x.Name == remoteName))
                {
                    if (_repository.Network.Remotes[remoteName].Url != url)
                    {
                        _repository.Network.Remotes.Remove(remoteName);
                        _repository.Network.Remotes.Add(remoteName, url);
                    }
                }
                else
                {
                    _repository.Network.Remotes.Add(remoteName, url);
                }

                var username = new Uri(url).UserInfo;
                if (username.Contains(":"))
                {
                    username = username.Substring(0, username.IndexOf(":"));
                }

                CredentialsHandler credentialHandler = (string url, string usernameFromUrl, SupportedCredentialTypes types) =>
                {
                    _logger.LogInformation($"Was asked to provide credentials for URL: {url}");
                    return new SshUserKeyCredentials
                    {
                        Username = username,
                        Passphrase = "",
                        PublicKey = publicKeyFile,
                        PrivateKey = privateKeyFile
                    };
                };

                string refspec;
                bool createBranch = false;
                if (new Regex("^[0-9a-f]{7,40}$").IsMatch(commit))
                {
                    // Fetch specific commit.
                    refspec = $"{commit}";
                    createBranch = true;
                }
                else
                {
                    refspec = string.Empty;
                    var refs = _repository.Network.ListReferences(url, credentialHandler).ToList();
                    foreach (var @ref in refs)
                    {
                        if (@ref.CanonicalName == $"refs/heads/{commit}")
                        {
                            refspec = $"refs/heads/{commit}:refs/heads/{commit}";
                            break;
                        }
                        else if (@ref.CanonicalName == $"refs/tags/{commit}")
                        {
                            refspec = $"refs/tags/{commit}:refs/tags/{commit}";
                            break;
                        }
                    }
                    if (refspec == string.Empty)
                    {
                        throw new NotFoundException($"The ref '{commit}' was not found in the remote repository. Check the branch and tag name and try again.");
                    }
                }

                // Add some well known paths onto our current PATH if they're not present. This helps
                // the search below and the invocation of 'ssh' when UEFS is running as a Kubernetes
                // HostProcess container.
                if (OperatingSystem.IsWindows())
                {
                    var wellKnownPaths = new HashSet<string>
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd").ToLowerInvariant(),
                        Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "System32", "OpenSSH").ToLowerInvariant()
                    };
                    var currentPaths = new HashSet<string>(Environment.GetEnvironmentVariable("PATH")?.ToLowerInvariant()?.Split(';') ?? new string[0]);
                    wellKnownPaths.ExceptWith(currentPaths);
                    foreach (var pathToAdd in wellKnownPaths)
                    {
                        if (Directory.Exists(pathToAdd))
                        {
                            var currentPathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(currentPathVariable))
                            {
                                currentPathVariable += ';';
                            }
                            currentPathVariable += pathToAdd;
                            Environment.SetEnvironmentVariable("PATH", currentPathVariable);
                        }
                    }
                }

                // If we have Git installed on the system, use that instead of libgit2 as libgit2 is
                // very slow at fetching large repositories (which Unreal Engine is).
                string? WhereSearch(string filename)
                {
                    var path = Environment.GetEnvironmentVariable("PATH");
                    var pathext = Environment.GetEnvironmentVariable("PATHEXT");
                    if (path == null || pathext == null)
                    {
                        return null;
                    }

                    var paths = new[] { Environment.CurrentDirectory }
                            .Concat(path.Split(';'));
                    var extensions = new[] { String.Empty }
                            .Concat(pathext.Split(';')
                                       .Where(e => e.StartsWith(".")));
                    var combinations = paths.SelectMany(x => extensions,
                            (path, extension) => Path.Combine(path, filename + extension));
                    return combinations.FirstOrDefault(File.Exists);
                }
                var nativeGitPath = WhereSearch("git.exe");
                if (nativeGitPath != null)
                {
                    var git = Process.Start(new ProcessStartInfo
                    {
                        Environment =
                        {
                            { "GIT_SSH_COMMAND", $"ssh -i {privateKeyFile.Replace("\\", "\\\\")} -o IdentitiesOnly=yes -o StrictHostKeyChecking=no" }
                        },
                        FileName = nativeGitPath,
                        WorkingDirectory = _gitRepoPath,
                        ArgumentList =
                        {
                            "fetch",
                            "--progress",
                            "--verbose",
                            remoteName,
                            refspec,
                        },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    });
                    if (git == null)
                    {
                        throw new InvalidOperationException("Failed to start Git process for performing fast fetch.");
                    }
                    _processSemaphore.WaitAsync();
                    try
                    {
                        _processes.Add(git);
                    }
                    finally
                    {
                        _processSemaphore.Release();
                    }
                    try
                    {
                        git.OutputDataReceived += (sender, args) =>
                        {
                            if (args.Data != null)// !string.IsNullOrWhiteSpace(args.Data))
                            {
                                _logger.LogInformation(args.Data.ToString().Trim());
                            }
                        };
                        var receiveRegex = new Regex("Receiving objects:\\s*(?<perc>[0-9]+)%\\s+\\((?<obj>[0-9]+)/(?<total>[0-9]+)\\),\\s+(?<data>[0-9\\.]+)\\s+(?<unit>[MKGTiB]+)\\s+\\|\\s+(?<rate>[0-9\\.]+)\\s+[MKGTiB/s]+$");
                        var receiveInitRegex = new Regex("Receiving objects:\\s*(?<perc>[0-9]+)%\\s+\\((?<obj>[0-9]+)/(?<total>[0-9]+)\\)$");
                        var receiveDoneRegex = new Regex("Receiving objects:\\s*(?<perc>[0-9]+)%\\s+\\((?<obj>[0-9]+)/(?<total>[0-9]+)\\),\\s+(?<data>[0-9\\.]+)\\s+(?<unit>[MKGTiB]+)\\s+\\|\\s+(?<rate>[0-9\\.]+)\\s+[MKGTiB/s]+,\\s+done$");
                        var deltaRegex = new Regex("Resolving deltas:\\s*(?<perc>[0-9]+)%\\s+\\((?<obj>[0-9]+)/(?<total>[0-9]+)\\)$");
                        var deltaDoneRegex = new Regex("Resolving deltas:\\s*(?<perc>[0-9]+)%\\s+\\((?<obj>[0-9]+)/(?<total>[0-9]+)\\),\\s+done$");
                        var dataHolder = new DataHolder();
                        git.ErrorDataReceived += (sender, args) =>
                        {
                            try
                            {
                                if (args.Data != null)
                                {
                                    if (args.Data.StartsWith("remote: "))
                                    {
                                        onProgress(new GitFetchProgressInfo
                                        {
                                            ServerProgressMessage = args.Data.Substring("remote: ".Length).Trim(),
                                            SlowFetch = false,
                                        });
                                    }
                                    else
                                    {
                                        var receiveMatch = receiveRegex.Match(args.Data.Trim());
                                        var receiveInitMatch = receiveInitRegex.Match(args.Data.Trim());
                                        var receiveDoneMatch = receiveDoneRegex.Match(args.Data.Trim());
                                        var deltaMatch = deltaRegex.Match(args.Data.Trim());
                                        var deltaDoneMatch = deltaDoneRegex.Match(args.Data.Trim());
                                        if (receiveDoneMatch.Success)
                                        {
                                            receiveMatch = receiveDoneMatch;
                                        }
                                        if (deltaDoneMatch.Success)
                                        {
                                            deltaMatch = deltaDoneMatch;
                                        }
                                        if (receiveMatch.Success)
                                        {
                                            var data = double.Parse(receiveMatch.Groups["data"].Value);
                                            long bytes = 0;
                                            switch (receiveMatch.Groups["unit"].Value)
                                            {
                                                case "KiB":
                                                    bytes = (long)Math.Ceiling(data * 1024);
                                                    break;
                                                case "MiB":
                                                    bytes = (long)Math.Ceiling(data * 1024 * 1024);
                                                    break;
                                                case "GiB":
                                                    bytes = (long)Math.Ceiling(data * 1024 * 1024 * 1024);
                                                    break;
                                                case "TiB":
                                                    bytes = (long)Math.Ceiling(data * 1024 * 1024 * 1024 * 1024);
                                                    break;
                                            }

                                            dataHolder.BytesReceived = bytes;
                                            onProgress(new GitFetchProgressInfo
                                            {
                                                TotalObjects = int.Parse(receiveMatch.Groups["total"].Value),
                                                IndexedObjects = 0,
                                                ReceivedObjects = int.Parse(receiveMatch.Groups["obj"].Value),
                                                ReceivedBytes = bytes,
                                                SlowFetch = false,
                                            });
                                        }
                                        else if (receiveInitMatch.Success)
                                        {
                                            onProgress(new GitFetchProgressInfo
                                            {
                                                TotalObjects = int.Parse(receiveInitMatch.Groups["total"].Value),
                                                IndexedObjects = 0,
                                                ReceivedObjects = int.Parse(receiveInitMatch.Groups["obj"].Value),
                                                ReceivedBytes = 0,
                                                SlowFetch = false,
                                            });
                                        }
                                        else if (deltaMatch.Success)
                                        {
                                            onProgress(new GitFetchProgressInfo
                                            {
                                                TotalObjects = int.Parse(deltaMatch.Groups["total"].Value),
                                                IndexedObjects = int.Parse(deltaMatch.Groups["obj"].Value),
                                                ReceivedObjects = int.Parse(deltaMatch.Groups["total"].Value),
                                                ReceivedBytes = dataHolder.BytesReceived,
                                                SlowFetch = false,
                                            });
                                        }
                                        else if (args.Data.Contains("[new tag]") || args.Data.Contains($"From {url}"))
                                        {
                                            // Do not emit.
                                        }
                                        else if (!string.IsNullOrWhiteSpace(args.Data))
                                        {
                                            _logger.LogWarning(args.Data.ToString().Trim());
                                        }
                                    }
                                }
                            }
                            catch (FormatException ex)
                            {
                                _logger.LogError(ex, ex.Message);
                            }
                        };
                        git.BeginOutputReadLine();
                        git.BeginErrorReadLine();
                        git.WaitForExit();
                        if (git.ExitCode != 0)
                        {
                            throw new InvalidOperationException($"Git process exited with non-zero exit code: {git.ExitCode}");
                        }
                        _logger.LogInformation("Git operation using native binary completed successfully.");
                    }
                    finally
                    {
                        _processSemaphore.WaitAsync();
                        try
                        {
                            _processes.Remove(git);
                        }
                        finally
                        {
                            _processSemaphore.Release();
                        }
                    }
                }
                else
                {
                    _repository.Network.Fetch(
                        remoteName,
                        new[]
                        {
                            refspec,
                        },
                        new FetchOptions
                        {
                            CertificateCheck = (Certificate certificate, bool valid, string host) =>
                            {
                                _logger.LogInformation("Was asked to verify if certificate was valid!");
                                return true;
                            },
                            CredentialsProvider = credentialHandler,
                            OnProgress = (string serverProgressOutput) =>
                            {
                                onProgress(new GitFetchProgressInfo
                                {
                                    ServerProgressMessage = serverProgressOutput,
                                    SlowFetch = true,
                                });
                                return true;
                            },
                            OnTransferProgress = (TransferProgress transferProgress) =>
                            {
                                onProgress(new GitFetchProgressInfo
                                {
                                    TotalObjects = transferProgress.TotalObjects,
                                    IndexedObjects = transferProgress.IndexedObjects,
                                    ReceivedObjects = transferProgress.ReceivedObjects,
                                    ReceivedBytes = transferProgress.ReceivedBytes,
                                    SlowFetch = true,
                                });
                                return true;
                            },
                            Prune = false,
                            RepositoryOperationStarting = (RepositoryOperationContext context) =>
                            {
                                _logger.LogInformation($"Repository operation starting: {context.RemoteUrl}");
                                return true;
                            },
                            RepositoryOperationCompleted = (RepositoryOperationContext context) =>
                            {
                                _logger.LogInformation($"Repository operation completed: {context.RemoteUrl}");
                            },
                            TagFetchMode = TagFetchMode.Auto,
                        },
                        "Updated ref from UEFS fetch");
                }

                if (createBranch)
                {
                    _repository.Branches.Add($"commit-{commit}", commit);
                }
                if (!HasCommit(commit))
                {
                    throw new InvalidOperationException("Internal fetch command failed to fetch commit!");
                }
            });
        }

        public void StopProcesses()
        {
            _processSemaphore.WaitAsync();
            try
            {
                foreach (var process in _processes)
                {
                    try
                    {
                        _logger.LogInformation($"Terminating Git process due to daemon shutdown: {process.Id} ...");
                        process.Kill();
                        _logger.LogInformation($"Terminated Git process due to daemon shutdown: {process.Id}");
                    }
                    catch
                    {
                        _logger.LogWarning($"Unable to kill running process during Git repo manager shutdown: {process.Id}");
                    }
                }
                _processes.Clear();
            }
            finally
            {
                _processSemaphore.Release();
            }
        }
    }
}
