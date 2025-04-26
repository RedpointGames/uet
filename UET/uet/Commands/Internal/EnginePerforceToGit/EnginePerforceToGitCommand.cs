namespace UET.Commands.Internal.EnginePerforceToGit
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class EnginePerforceToGitCommand
    {
        internal sealed class Options
        {
            public Option<string> P4Client;
            public Option<string> P4Tickets;
            public Option<string> P4Config;
            public Option<string> P4Port;
            public Option<string> P4Host;
            public Option<string> P4User;
            public Option<string> P4Trust;
            public Option<DirectoryInfo> P4WorkspacePath;

            public Option<string> GitRepositoryUri;
            public Option<DirectoryInfo> GitWorkspacePath;

            public Options()
            {
                P4Client = new Option<string>(
                    "--p4-client",
                    description: "The value of P4CLIENT, also known as the 'Workspace' in P4V.")
                {
                    IsRequired = true,
                };

                P4Tickets = new Option<string>(
                    "--p4-tickets",
                    description: "The value of P4TICKETS, which keeps track of the authentication tickets between runs. This path doesn't have to initially exist.")
                {
                    IsRequired = true,
                };

                P4Config = new Option<string>(
                    "--p4-config",
                    description: "The value of P4CONFIG. Optional.");

                P4Port = new Option<string>(
                    "--p4-port",
                    description: "The value of P4PORT. Despite it's name, this is the full IP address and port of the Perforce server to connect to, such as '<ip address>:<port>'.")
                {
                    IsRequired = true,
                };

                P4Host = new Option<string>(
                    "--p4-host",
                    description: "The value of P4HOST. Despite it's name, this is the hostname of the local machine that owns the workspace defined in P4CLIENT. This should be set to a consistent value if you're running this across many build machines.")
                {
                    IsRequired = true,
                };

                P4User = new Option<string>(
                    "--p4-user",
                    description: "The value of P4USER. This is the username used to connect to the Perforce server. You must also have the P4PASSWD environment variable set before running this command.")
                {
                    IsRequired = true,
                };

                P4Trust = new Option<string>(
                    "--p4-trust",
                    description: "The value of P4TRUST, which indicates the certificate hashes of the Perforce server to connect to. This must be initialized ahead of time and already exist, otherwise Perforce commands will fail with certificate errors.")
                {
                    IsRequired = true,
                };

                P4WorkspacePath = new Option<DirectoryInfo>(
                    "--p4-workspace-path",
                    description: "The path that the workspace in P4CLIENT is configured to check out to on the 'local machine'. This is typically a network share. You must have set up the workspace ahead of time in P4V.")
                {
                    IsRequired = true,
                };

                GitRepositoryUri = new Option<string>(
                    "--git-repository-uri",
                    description: "The URI of the Git repository that Perforce snapshots will be synced to.")
                {
                    IsRequired = true,
                };

                GitWorkspacePath = new Option<DirectoryInfo>(
                    "--git-workspace-path",
                    description: "The path that the Git repository will be checked out to. This should be a persistent path on a network share to avoid long checkout times.")
                {
                    IsRequired = true,
                };
            }
        }

        public static Command CreateEnginePerforceToGitCommand()
        {
            var options = new Options();
            var command = new Command("engine-perforce-to-git", "Synchronise snapshots from Perforce into a Git repository with large files stored in Git LFS.")
            {
                FullDescription =
                """
                If you need a reference for workspace mappings to use with this tool:
                
                - Open P4V, go to View -> Workspaces.
                - In the Filter, replace "Owner" with "Name" and set "RedpointGames_Latest" as the value.
                - You should see the "RedpointGames_Latest" workspace in the results.
                - Right-click on it, 'View Workspace'.
                - Copy the Options and View sections to Notepad, and use them to set up your own workspace.
                """
            };
            command.AddAllOptions(options);
            command.AddCommonHandler<EnginePerforceToGitCommandInstance>(options);
            return command;
        }

        private sealed class EnginePerforceToGitCommandInstance : ICommandInstance
        {
            private readonly ILogger<EnginePerforceToGitCommandInstance> _logger;
            private readonly IPathResolver _pathResolver;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public EnginePerforceToGitCommandInstance(
                ILogger<EnginePerforceToGitCommandInstance> logger,
                IPathResolver pathResolver,
                IProcessExecutor processExecutor,
                Options options)
            {
                _logger = logger;
                _pathResolver = pathResolver;
                _processExecutor = processExecutor;
                _options = options;
            }

            private class FeedPasswordCaptureSpecification : ICaptureSpecification
            {
                public bool InterceptStandardInput => true;

                public bool InterceptStandardOutput => false;

                public bool InterceptStandardError => false;

                public void OnReceiveStandardError(string data)
                {
                    throw new NotImplementedException();
                }

                public void OnReceiveStandardOutput(string data)
                {
                    throw new NotImplementedException();
                }

                public string? OnRequestStandardInputAtStartup()
                {
                    return $"{Environment.GetEnvironmentVariable("P4PASSWD")}\n";
                }
            }

            private static void RemoveIndexLock(DirectoryInfo gitDirectoryInfo)
            {
                if (File.Exists(Path.Combine(gitDirectoryInfo.FullName, ".git", "index.lock")))
                {
                    File.Delete(Path.Combine(gitDirectoryInfo.FullName, ".git", "index.lock"));
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var p4Client = context.ParseResult.GetValueForOption(_options.P4Client) ?? string.Empty;
                var p4Tickets = context.ParseResult.GetValueForOption(_options.P4Tickets) ?? string.Empty;
                var p4Config = context.ParseResult.GetValueForOption(_options.P4Config) ?? string.Empty;
                var p4Port = context.ParseResult.GetValueForOption(_options.P4Port) ?? string.Empty;
                var p4Host = context.ParseResult.GetValueForOption(_options.P4Host)! ?? string.Empty;
                var p4User = context.ParseResult.GetValueForOption(_options.P4User) ?? string.Empty;
                var p4Trust = context.ParseResult.GetValueForOption(_options.P4Trust) ?? string.Empty;
                var p4WorkspacePath = context.ParseResult.GetValueForOption(_options.P4WorkspacePath)!;
                var gitRepositoryUri = context.ParseResult.GetValueForOption(_options.GitRepositoryUri) ?? string.Empty;
                var gitWorkspacePath = context.ParseResult.GetValueForOption(_options.GitWorkspacePath)!;

                var p4Passwd = Environment.GetEnvironmentVariable("P4PASSWD");
                if (string.IsNullOrWhiteSpace(p4Passwd))
                {
                    _logger.LogError("The 'P4PASSWD' environment variable must be set.");
                    return 1;
                }

                _logger.LogInformation($"--p4-client:               {p4Client}");
                _logger.LogInformation($"--p4-tickets:              {p4Tickets}");
                _logger.LogInformation($"--p4-config:               {p4Config}");
                _logger.LogInformation($"--p4-port:                 {p4Port}");
                _logger.LogInformation($"--p4-host:                 {p4Host}");
                _logger.LogInformation($"--p4-user:                 {p4User}");
                _logger.LogInformation($"--p4-trust:                {p4Trust}");
                _logger.LogInformation($"--p4-workspace-path:       {p4WorkspacePath.FullName}");
                _logger.LogInformation($"--git-repository-uri:      {gitRepositoryUri}");
                _logger.LogInformation($"--git-workspace-path:      {gitWorkspacePath.FullName}");

                var p4Envs = new Dictionary<string, string>
                {
                    { "P4CLIENT", p4Client },
                    { "P4TICKETS", p4Tickets },
                    { "P4CONFIG", p4Config },
                    { "P4PORT", p4Port },
                    { "P4HOST", p4Host },
                    { "P4USER", p4User },
                    { "P4TRUST", p4Trust },
                };

                var gitEnvs = new Dictionary<string, string>
                {
                    { "GIT_ASK_YESNO", "false" },
                };

                _logger.LogInformation("Locating p4 binary...");
                string? p4 = Path.Combine(Environment.CurrentDirectory, OperatingSystem.IsWindows() ? "p4.exe" : "p4");
                if (!File.Exists(p4))
                {
                    p4 = null;
                    try
                    {
                        p4 = await _pathResolver.ResolveBinaryPath("p4");
                    }
                    catch
                    {
                    }
                }
                if (p4 == null)
                {
                    _logger.LogError($"Unable to locate p4 in current directory or on PATH.");
                    return 1;
                }

                _logger.LogInformation("Locating Git binary...");
                string? git = null;
                try
                {
                    git = await _pathResolver.ResolveBinaryPath("git");
                }
                catch
                {
                }
                if (git == null)
                {
                    _logger.LogError($"Unable to locate git on PATH.");
                    return 1;
                }

                string? robocopy = null;
                string? rclone = null;
                if (OperatingSystem.IsWindows())
                {
                    _logger.LogInformation("Locating Robocopy binary...");
                    try
                    {
                        robocopy = await _pathResolver.ResolveBinaryPath("robocopy");
                    }
                    catch
                    {
                    }
                    if (robocopy == null)
                    {
                        _logger.LogError($"Unable to locate robocopy on PATH.");
                        return 1;
                    }
                }
                else
                {
                    _logger.LogInformation("Locating rclone binary...");
                    try
                    {
                        rclone = await _pathResolver.ResolveBinaryPath("rclone");
                    }
                    catch
                    {
                    }
                    if (rclone == null)
                    {
                        _logger.LogError($"Unable to locate rclone on PATH.");
                        return 1;
                    }
                }

                _logger.LogInformation("Signing out of Perforce server...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = p4,
                        Arguments = ["-I", "logout"],
                        EnvironmentVariables = p4Envs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());

                _logger.LogInformation("Signing into Perforce server...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = p4,
                        Arguments = ["-I", "login"],
                        EnvironmentVariables = p4Envs,
                    },
                    new FeedPasswordCaptureSpecification(),
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to sign into Perforce server.");
                    return exitCode;
                }

                _logger.LogInformation("Syncing latest Perforce content to client...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = p4,
                        Arguments = ["-I", "sync", "--parallel=24", $"//{p4Client}/..."],
                        EnvironmentVariables = p4Envs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to sync Perforce content (phase 1).");
                    return exitCode;
                }

                _logger.LogInformation("Syncing latest Perforce content to workspace path...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = p4,
                        Arguments = ["-I", "sync", "--parallel=24", $"{p4WorkspacePath}{Path.DirectorySeparatorChar}...#head"],
                        EnvironmentVariables = p4Envs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to sync Perforce content (phase 2).");
                    return exitCode;
                }

                _logger.LogInformation("Reconciling Perforce workspace in case files don't exactly match...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = p4,
                        Arguments = ["clean"],
                        EnvironmentVariables = p4Envs,
                        WorkingDirectory = p4WorkspacePath.FullName,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to reconcile Perforce content.");
                    return exitCode;
                }

                _logger.LogInformation("Turning off 'safe.directory' setting for Git...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = ["config", "--global", "--replace-all", "safe.directory", "*"],
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to change safe.directory configuration setting.");
                    return exitCode;
                }

                _logger.LogInformation("Making sure Git LFS is globally installed...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = ["lfs", "install"],
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to set up Git LFS.");
                    return exitCode;
                }

                _logger.LogInformation("Setting up Git workspace...");
                RemoveIndexLock(gitWorkspacePath);
                if (!Directory.Exists(Path.Combine(gitWorkspacePath.FullName, ".git")))
                {
                    gitWorkspacePath.Create();

                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["init", gitWorkspacePath.FullName],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError("Failed to init Git repository.");
                        return exitCode;
                    }
                }

                async Task<int> ChangeConfig(string name, string value)
                {
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["config", name, value],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to change {name} configuration setting.");
                    }
                    return exitCode;
                }

                _logger.LogInformation("Setting author information for commits...");
                exitCode = await ChangeConfig("user.email", "uet-p4-sync@redpoint.games");
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await ChangeConfig("user.name", "UET Perforce to Git");
                if (exitCode != 0)
                {
                    return exitCode;
                }

                _logger.LogInformation("Increasing LFS timeouts and retries...");
                exitCode = await ChangeConfig("lfs.dialtimeout", "3600");
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await ChangeConfig("lfs.activitytimeout", "3600");
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await ChangeConfig("lfs.tlstimeout", "3600");
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await ChangeConfig("lfs.transfer.maxretries", "32");
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await ChangeConfig("lfs.transfer.maxverifies", "32");
                if (exitCode != 0)
                {
                    return exitCode;
                }

                _logger.LogInformation("Setting remote URI...");
                RemoveIndexLock(gitWorkspacePath);
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = ["remote", "set-url", "origin", gitRepositoryUri],
                        WorkingDirectory = gitWorkspacePath.FullName,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["remote", "add", "origin", gitRepositoryUri],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError("Failed to set up Git remote.");
                        return exitCode;
                    }
                }

                _logger.LogInformation("Fetching all existing branches from Git that start with '5.'...");
                RemoveIndexLock(gitWorkspacePath);
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = git,
                        Arguments = ["fetch", "origin", "+refs/heads/5.*:refs/remotes/origin/5.*"],
                        WorkingDirectory = gitWorkspacePath.FullName,
                        EnvironmentVariables = gitEnvs,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    _logger.LogError("Failed to fetch Git branches.");
                    return exitCode;
                }

                _logger.LogInformation("Iterating through folders in Perforce that start with 'Release-5.'...");
                foreach (var releaseFolder in new DirectoryInfo(Path.Combine(p4WorkspacePath.FullName, "UE5")).GetDirectories("Release-5.*"))
                {
                    var releaseVersion = releaseFolder.Name.Substring("Release-".Length);

                    _logger.LogInformation($"Checking if Git branch 'origin/{releaseVersion}' exists...");
                    RemoveIndexLock(gitWorkspacePath);
                    var revision = new StringBuilder();
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["rev-parse", "--verify", $"origin/{releaseVersion}"],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(revision),
                        context.GetCancellationToken());

                    var isNew = exitCode != 0;

                    _logger.LogInformation($"Preparing branch '{releaseVersion}'...");
                    if (isNew)
                    {
                        var baseVersionNumber = new EngineVersionNumber(releaseVersion);
                        baseVersionNumber.Minus(1);

                        // Try to find a previous branch that we can start this branch at, so that merging to a new engine version will let Git handle the merge properly.
                        string? baseGitBranch = null;
                        while (baseVersionNumber.Major >= 5)
                        {
                            var baseRevision = new StringBuilder();
                            exitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = ["rev-parse", "--verify", $"origin/{baseVersionNumber.Major}.{baseVersionNumber.Minor}"],
                                    WorkingDirectory = gitWorkspacePath.FullName,
                                    EnvironmentVariables = gitEnvs,
                                },
                                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(baseRevision),
                                context.GetCancellationToken());
                            if (exitCode == 0)
                            {
                                baseGitBranch = baseRevision.ToString().Trim();
                                break;
                            }
                            else
                            {
                                baseVersionNumber.Minus(1);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(baseGitBranch))
                        {
                            _logger.LogInformation($"Creating new branch '{releaseVersion}' at revision '{baseGitBranch}'...");
                            RemoveIndexLock(gitWorkspacePath);
                            exitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = ["checkout", "-B", releaseVersion, baseGitBranch],
                                    WorkingDirectory = gitWorkspacePath.FullName,
                                    EnvironmentVariables = gitEnvs,
                                },
                                CaptureSpecification.Passthrough,
                                context.GetCancellationToken());
                            if (exitCode != 0)
                            {
                                _logger.LogError($"Failed to create new branch '{releaseVersion}' at revision '{baseGitBranch}'.");
                                return exitCode;
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Creating new branch '{releaseVersion}'...");
                            RemoveIndexLock(gitWorkspacePath);
                            exitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = git,
                                    Arguments = ["checkout", "--orphan", releaseVersion],
                                    WorkingDirectory = gitWorkspacePath.FullName,
                                    EnvironmentVariables = gitEnvs,
                                },
                                CaptureSpecification.Passthrough,
                                context.GetCancellationToken());
                            if (exitCode != 0)
                            {
                                _logger.LogError($"Failed to create new branch '{releaseVersion}'.");
                                return exitCode;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Git LFS fetch from 'origin/{releaseVersion}'...");
                        RemoveIndexLock(gitWorkspacePath);
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = ["lfs", "fetch", "origin", revision.ToString().Trim()],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Failed to Git LFS fetch '{releaseVersion}'.");
                            return exitCode;
                        }

                        _logger.LogInformation($"Switch HEAD to 'origin/{releaseVersion}'...");
                        RemoveIndexLock(gitWorkspacePath);
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = ["symbolic-ref", "HEAD", $"refs/remotes/origin/{releaseVersion}"],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Failed to switch HEAD to 'origin/{releaseVersion}'.");
                            return exitCode;
                        }

                        _logger.LogInformation($"Reset/create branch '{releaseVersion}' to 'origin/{releaseVersion}'...");
                        RemoveIndexLock(gitWorkspacePath);
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = ["update-ref", $"refs/heads/{releaseVersion}", $"refs/remotes/origin/{releaseVersion}"],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Failed to set ref of '{releaseVersion}' to 'origin/{releaseVersion}'.");
                            return exitCode;
                        }

                        _logger.LogInformation($"Switch HEAD to '{releaseVersion}'...");
                        RemoveIndexLock(gitWorkspacePath);
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = git,
                                Arguments = ["symbolic-ref", "HEAD", $"refs/heads/{releaseVersion}"],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Failed to switch HEAD to 'origin/{releaseVersion}'.");
                            return exitCode;
                        }
                    }

                    void DeleteAllGitModulesAndAttributes()
                    {
                        _logger.LogInformation($"Deleting all .gitattributes, .gitmodules and .gitignore files...");
                        foreach (var file in gitWorkspacePath.EnumerateFiles("*", SearchOption.AllDirectories))
                        {
                            if (file.Name == ".gitignore" ||
                                file.Name == ".gitmodules" ||
                                file.Name == ".gitattributes")
                            {
                                _logger.LogInformation($"  '{file.FullName}'...");
                                File.Delete(file.FullName);
                            }
                        }
                    }

                    DeleteAllGitModulesAndAttributes();

                    if (OperatingSystem.IsWindows())
                    {
                        _logger.LogInformation($"Using robocopy to mirror everything into Git...");
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = robocopy!,
                                Arguments = ["/MIR", releaseFolder.FullName, gitWorkspacePath.FullName, "/XD", ".git", "/XJ", "/NJH", "/ETA", "/MT:128"],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Sanitized,
                            context.GetCancellationToken());
                        if (exitCode > 8)
                        {
                            _logger.LogError($"Failed to robocopy.");
                            return exitCode;
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Using rclone to mirror everything into Git...");
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = rclone!,
                                Arguments = [
                                    "sync",
                                    "--exclude=/.git/**",
                                    "--transfers=64",
                                    "--delete-before",
                                    releaseFolder.FullName,
                                    gitWorkspacePath.FullName,
                                ],
                                WorkingDirectory = gitWorkspacePath.FullName,
                                EnvironmentVariables = gitEnvs,
                            },
                            CaptureSpecification.Sanitized,
                            context.GetCancellationToken());
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Failed to rsync.");
                            return exitCode;
                        }
                    }

                    DeleteAllGitModulesAndAttributes();

                    _logger.LogInformation($"Setting .gitattributes...");
                    File.WriteAllText(
                        Path.Combine(gitWorkspacePath.FullName, ".gitattributes"),
                        """
                        *.dll filter=lfs diff=lfs merge=lfs -text
                        *.so filter=lfs diff=lfs merge=lfs -text
                        *.dylib filter=lfs diff=lfs merge=lfs -text
                        *.pdb filter=lfs diff=lfs merge=lfs -text
                        *.exe filter=lfs diff=lfs merge=lfs -text
                        *.uasset filter=lfs diff=lfs merge=lfs -text
                        *.a filter=lfs diff=lfs merge=lfs -text
                        *.png filter=lfs diff=lfs merge=lfs -text
                        *.svg filter=lfs diff=lfs merge=lfs -text
                        **/Binaries/** filter=lfs diff=lfs merge=lfs -text
                        **/Content/** filter=lfs diff=lfs merge=lfs -text
                        **/ThirdParty/** filter=lfs diff=lfs merge=lfs -text
                        **/Documentation/** filter=lfs diff=lfs merge=lfs -text
                        Engine/Extras/** filter=lfs diff=lfs merge=lfs -text
                        """);

                    _logger.LogInformation($"Staging all changes into Git...");
                    RemoveIndexLock(gitWorkspacePath);
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["add", "-A"],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to stage all changes into Git.");
                        return exitCode;
                    }

                    _logger.LogInformation("Getting revision information from Perforce...");
                    var perforceMessage = new StringBuilder();
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = p4,
                            Arguments = ["-I", "changes", "-m1", $"//{p4Client}/UE5/{releaseFolder.Name}/...#head"],
                            EnvironmentVariables = p4Envs,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(perforceMessage),
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError("Failed to get Perforce revision information.");
                        return exitCode;
                    }
                    var commitMessage = perforceMessage.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(commitMessage))
                    {
                        commitMessage = $"Automatic snapshot of Perforce to Git for Unreal Engine {releaseVersion}.";
                    }
                    _logger.LogInformation($"Commit message for Git: {commitMessage}");

                    _logger.LogInformation($"Committing all changes into Git...");
                    RemoveIndexLock(gitWorkspacePath);
                    var commitOutput = new StringBuilder();
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["commit", "-m", commitMessage],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(commitOutput),
                        context.GetCancellationToken());
                    var commitOutputString = commitOutput.ToString();
                    if (exitCode != 0)
                    {
                        // If we fail to commit because there's nothing to commit, that's fine.
                        if (!commitOutputString.Contains("nothing to commit, working tree clean", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(commitOutputString);
                            _logger.LogError($"Failed to commit changes to Git.");
                            return exitCode;
                        }
                    }
                    Console.WriteLine(commitOutputString);

                    _logger.LogInformation($"Pushing changes to origin...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = git,
                            Arguments = ["push", "origin", releaseVersion],
                            WorkingDirectory = gitWorkspacePath.FullName,
                            EnvironmentVariables = gitEnvs,
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to push changes back to Git.");
                        return exitCode;
                    }
                }

                _logger.LogInformation("All work complete!");
                return 0;
            }
        }
    }
}
