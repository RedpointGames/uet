namespace UET.Commands.EngineSpec
{
    using Redpoint.Registry;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Specialized;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Web;

    internal sealed class EngineSpec
    {
        private EngineSpec()
        {
        }

        private static Regex _versionRegex = new Regex("^[45]\\.[0-9]+(EA)?$");

        public static ParseArgument<EngineSpec> ParseEngineSpec(
            Option<PathSpec[]> pathSpecs,
            Option<DistributionSpec?>? distributionOpt)
        {
            return ParseEngineSpec(
                result => (result.GetValueForOption(pathSpecs) ?? Array.Empty<PathSpec>()).FirstOrDefault(),
                pathSpecs.Name,
                distributionOpt);
        }

        public static ParseArgument<EngineSpec> ParseEngineSpec(
            Option<PathSpec> pathSpec,
            Option<DistributionSpec?>? distributionOpt)
        {
            return ParseEngineSpec(
                result => result.GetValueForOption(pathSpec),
                pathSpec.Name,
                distributionOpt);
        }

        public static ParseArgument<EngineSpec> ParseEngineSpecContextless()
        {
            return ParseEngineSpec(
                null,
                string.Empty,
                null);
        }

        private static ParseArgument<EngineSpec> ParseEngineSpec(
            Func<ArgumentResult, PathSpec?>? getPathSpec,
            string pathSpecOptionName,
            Option<DistributionSpec?>? distributionOpt)
        {
            return (result) =>
            {
                // If the engine is specified, use it.
                if (result.Tokens.Count > 0)
                {
                    return ParseEngineSpecWithoutPath(result);
                }

                // If the current directory contains an engine, use the current
                // directory. The intent in this case is clearly preferencing the
                // engine folder you're working out of over any auto-detected engine
                // specified in project files.
                var buildVersion = System.IO.Path.Combine(Environment.CurrentDirectory, "Engine", "Build", "Build.version");
                if (File.Exists(buildVersion))
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.Path,
                        OriginalSpec = string.Empty,
                        Path = Environment.CurrentDirectory,
                    };
                }

                // Otherwise, take a look at the path spec value to see if we
                // can figure out the target engine from the project file.
                PathSpec? path = null;
                DistributionSpec? distribution = null;
                if (getPathSpec != null)
                {
                    try
                    {
                        path = getPathSpec(result);
                    }
                    catch (InvalidOperationException)
                    {
                        return null!;
                    }
                }
                if (distributionOpt != null)
                {
                    try
                    {
                        distribution = result.GetValueForOption(distributionOpt);
                    }
                    catch (InvalidOperationException)
                    {
                        return null!;
                    }
                }
                if (path == null)
                {
                    if (getPathSpec == null)
                    {
                        result.ErrorMessage = $"You must explicitly set the engine version to use with --{result.Argument.Name}.";
                    }
                    else
                    {
                        result.ErrorMessage = $"Can't automatically detect the appropriate engine because the --{pathSpecOptionName} option was invalid.";
                    }
                    return null!;
                }
                switch (path.Type)
                {
                    case PathSpecType.UProject:
                        // Read the .uproject file as JSON and get the engine value from it.
                        using (var uprojectFileStream = new FileStream(path.UProjectPath!, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var projectFile = JsonSerializer.Deserialize<UProjectFile>(
                                uprojectFileStream,
                                SourceGenerationContext.Default.UProjectFile);
                            if (projectFile?.EngineAssociation != null)
                            {
                                var engineSpec = TryParseEngine(projectFile.EngineAssociation, EngineParseFlags.WindowsRegistry | EngineParseFlags.WindowsFolder | EngineParseFlags.MacFolder);
                                if (engineSpec == null)
                                {
                                    result.ErrorMessage = $"The '.uproject' file specifies an engine that is not installed or can't be found ({projectFile.EngineAssociation}).";
                                    return null!;
                                }
                                return engineSpec;
                            }
                            result.ErrorMessage = $"The '.uproject' file does not specify an engine via EngineAssociation; use --{result.Argument.Name} to specify the engine instead.";
                            return null!;
                        }
                    case PathSpecType.UPlugin:
                        // Can't automatically infer the engine version for plugins.
                        result.ErrorMessage = $"The engine version can not be inferred automatically for plugins; use --{result.Argument.Name} to specify the engine instead.";
                        return null!;
                    case PathSpecType.BuildConfig:
                        // If this build configuration is for an engine, then return SelfEngine.
                        var selectedEngineDistribution = distribution?.Distribution as BuildConfigEngineDistribution;
                        if (selectedEngineDistribution != null)
                        {
                            return new EngineSpec
                            {
                                OriginalSpec = string.Empty,
                                Type = EngineSpecType.SelfEngineByBuildConfig,
                            };
                        }

                        // If this build configuration is for a project, determine which project file based on
                        // the distribution and then read the engine association from that project file.
                        var selectedProjectDistribution = distribution?.Distribution as BuildConfigProjectDistribution;
                        if (selectedProjectDistribution == null || distributionOpt == null)
                        {
                            if (distribution?.Distribution == null)
                            {
                                result.ErrorMessage = $"The engine version can not be inferred automatically; use --{result.Argument.Name} to specify the engine instead.";
                            }
                            else
                            {
                                result.ErrorMessage = $"The engine version can not be inferred automatically for plugins; use --{result.Argument.Name} to specify the engine instead.";
                            }
                            return null!;
                        }

                        var uprojectPath = System.IO.Path.Combine(path.DirectoryPath, selectedProjectDistribution.FolderName, $"{selectedProjectDistribution.ProjectName}.uproject");
                        if (!File.Exists(uprojectPath))
                        {
                            result.ErrorMessage = $"The distribution '{distribution}' specified by --{distributionOpt.Name} refers to the project file '{uprojectPath}', but this project file does not exist on disk, so the engine version can not be inferred.";
                            return null!;
                        }

                        using (var uprojectFileStream = new FileStream(uprojectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var projectFile = JsonSerializer.Deserialize<UProjectFile>(
                                uprojectFileStream,
                                SourceGenerationContext.Default.UProjectFile);
                            if (projectFile?.EngineAssociation != null)
                            {
                                var engineSpec = TryParseEngine(projectFile.EngineAssociation, EngineParseFlags.WindowsRegistry | EngineParseFlags.WindowsFolder | EngineParseFlags.MacFolder);
                                if (engineSpec == null)
                                {
                                    result.ErrorMessage = $"The '.uproject' file (referred to by the '{distribution}' distribution) specifies an engine that is not installed or can't be found ({projectFile.EngineAssociation}).";
                                    return null!;
                                }
                                return engineSpec;
                            }
                            result.ErrorMessage = $"The '.uproject' file (referred to by the '{distribution}' distribution) does not specify an engine via EngineAssociation; use --{result.Argument.Name} to specify the engine instead.";
                            return null!;
                        }
                }

                result.ErrorMessage = $"Can't automatically detect the appropriate engine because the --{pathSpecOptionName} option was invalid.";
                return null!;
            };
        }

        [Flags]
        private enum EngineParseFlags
        {
            None = 0,

            UEFS = 1 << 0,
            WindowsRegistry = 1 << 1,
            WindowsFolder = 1 << 2,
            MacFolder = 1 << 3,
            AbsolutePath = 1 << 4,
            Git = 1 << 5,
            LauncherInstalled = 1 << 6,
            WindowsUserRegistry = 1 << 7,
            SESNetworkShare = 1 << 8,
            RemoteZfs = 1 << 9,
            CurrentWorkspace = 1 << 10,

            All = 0xFFFFFF,
        }

        public static EngineSpec? TryParseEngineSpecExact(string engine)
        {
            return TryParseEngine(engine);
        }

        private static EngineSpec? TryParseEngine(string engine, EngineParseFlags flags = EngineParseFlags.All)
        {
            if ((flags & EngineParseFlags.CurrentWorkspace) != 0)
            {
                // Detect UEFS tags.
                if (engine == "self:true")
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.CurrentWorkspace,
                        OriginalSpec = engine,
                    };
                }
            }

            if ((flags & EngineParseFlags.UEFS) != 0)
            {
                // Detect UEFS tags.
                if (engine.StartsWith("uefs:", StringComparison.Ordinal))
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.UEFSPackageTag,
                        OriginalSpec = engine,
                        UEFSPackageTag = engine["uefs:".Length..],
                    };
                }
            }

            if ((flags & EngineParseFlags.SESNetworkShare) != 0)
            {
                if (engine.StartsWith("ses:", StringComparison.Ordinal))
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.SESNetworkShare,
                        OriginalSpec = engine,
                        // @note: Allow '\' to be passed as '/' to avoid weird escaping on build servers and command line.
                        SESNetworkShare = engine["ses:".Length..].Replace('/', '\\'),
                    };
                }
            }

            if ((flags & EngineParseFlags.RemoteZfs) != 0)
            {
                if (engine.StartsWith("rzfs:", StringComparison.Ordinal))
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.RemoteZfs,
                        OriginalSpec = engine,
                        RemoteZfs = engine["rzfs:".Length..],
                    };
                }
            }

            if ((flags & EngineParseFlags.Git) != 0)
            {
                // Detect commits.
                if (engine.StartsWith("git:", StringComparison.Ordinal))
                {
                    // <commit>@<url>,f:<folder>,z:<zip>,...
                    var value = engine["git:".Length..];
                    var firstAt = value.IndexOf('@', StringComparison.Ordinal);
                    var commit = value[..firstAt];
                    value = value[(firstAt + 1)..];

                    var firstQuestionMark = value.IndexOf('?', StringComparison.Ordinal);
                    string url;
                    string? configString = null;
                    NameValueCollection qs;
                    if (firstQuestionMark != -1)
                    {
                        url = value[..firstQuestionMark];
                        qs = HttpUtility.ParseQueryString(value[(firstQuestionMark + 1)..]);
                        if (qs["config"] != null)
                        {
                            configString = qs["config"];
                        }
                    }
                    else
                    {
                        var firstComma = value.IndexOf(',', StringComparison.Ordinal);
                        url = firstComma == -1 ? value : value[..firstComma];
                        if (firstComma != -1)
                        {
                            configString = value[(firstComma + 1)..];
                        }
                        qs = new NameValueCollection();
                    }

                    (string type, string value)[] layers;
                    if (configString != null)
                    {
                        layers = configString.Split(',').Select(x =>
                        {
                            var s = x.Split(':', 2);
                            return (s[0], s[1]);
                        }).ToArray();
                    }
                    else
                    {
                        layers = Array.Empty<(string type, string value)>();
                    }

                    // @note: Folders aren't used yet.
                    var folders = layers.Where(x => x.type == "f").Select(x => x.value).ToArray();
                    var zips = layers.Where(x => x.type == "z").Select(x => x.value).ToArray();
                    var windowsSharedGitCachePath = layers.Where(x => x.type == "wc").Select(x => x.value).FirstOrDefault();
                    var macSharedGitCachePath = layers.Where(x => x.type == "mc").Select(x => x.value).FirstOrDefault();

                    return new EngineSpec
                    {
                        Type = EngineSpecType.GitCommit,
                        OriginalSpec = engine,
                        GitUrl = url,
                        GitCommit = commit,
                        FolderLayers = folders,
                        ZipLayers = zips,
                        WindowsSharedGitCachePath = windowsSharedGitCachePath,
                        MacSharedGitCachePath = macSharedGitCachePath,
                        GitQueryString = qs,
                    };
                }
            }

            if ((flags & EngineParseFlags.LauncherInstalled) != 0)
            {
                // This matches the path behaviour specified by the engine in:
                //
                // - Engine/Source/Developer/DesktopPlatform/Private/DesktopPlatformBase.cpp (FDesktopPlatformBase::ReadLauncherInstallationList)
                // - Engine/Source/Runtime/Core/Private/Windows/WindowsPlatformProcess.cpp (FWindowsPlatformProcess::ApplicationSettingsDir)
                // - Engine/Source/Runtime/Core/Private/Mac/MacPlatformProcess.cpp (FMacPlatformProcess::ApplicationSettingsDir)
                //
                var launcherInstalled = true switch
                {
                    var b when b == OperatingSystem.IsWindows() => System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Epic",
                        "UnrealEngineLauncher",
                        "LauncherInstalled.dat"),
                    var b when b == OperatingSystem.IsMacOS() => System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Library",
                        "Application Support",
                        "Epic",
                        "UnrealEngineLauncher",
                        "LauncherInstalled.dat"),
                    _ => string.Empty,
                };
                if (!string.IsNullOrWhiteSpace(launcherInstalled) &&
                    File.Exists(launcherInstalled))
                {
                    try
                    {
                        var installed = JsonSerializer.Deserialize(File.ReadAllText(launcherInstalled), SourceGenerationContext.Default.LauncherInstalled);
                        if (installed?.InstallationList != null)
                        {
                            foreach (var installation in installed.InstallationList)
                            {
                                if (installation.AppName?.StartsWith($"UE_{engine}", StringComparison.Ordinal) ?? false &&
                                    installation.InstallLocation != null &&
                                    Directory.Exists(installation.InstallLocation))
                                {
                                    // We've found the engine via LauncherInstalled.dat.
                                    return new EngineSpec
                                    {
                                        Type = EngineSpecType.Version,
                                        Version = engine,
                                        OriginalSpec = engine,
                                        Path = installation.InstallLocation,
                                    };
                                }
                            }
                        }
                    }
                    catch
                    {
                        // LauncherInstalled.dat is somehow invalid. Ignore it.
                    }
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if ((flags & EngineParseFlags.WindowsUserRegistry) != 0)
                {
                    // This matches the registry behaviour specified by the engine in:
                    //
                    // - Engine/Source/Developer/DesktopPlatform/Private/Windows/DesktopPlatformWindows.cpp (FDesktopPlatformWindows::EnumerateEngineInstallations)
                    //
                    using (var stack = RegistryStack.OpenPath($@"HKCU:\SOFTWARE\Epic Games\Unreal Engine\Builds"))
                    {
                        if (stack.Exists)
                        {
                            foreach (var engineName in stack.Key.GetValueNames())
                            {
                                if (engine.Equals(engineName, StringComparison.OrdinalIgnoreCase))
                                {
                                    var registryBasedPath = stack.Key.GetValue(engineName) as string;
                                    if (registryBasedPath != null && Directory.Exists(registryBasedPath))
                                    {
                                        return new EngineSpec
                                        {
                                            Type = EngineSpecType.Version,
                                            Version = engine,
                                            OriginalSpec = engine,
                                            Path = registryBasedPath,
                                        };
                                    }
                                }
                            }
                        }
                    }
                }

                if ((flags & EngineParseFlags.WindowsRegistry) != 0)
                {
                    // @note: This registry key isn't accessed by the engine itself, but seems to be set by the launcher.
                    using (var stack = RegistryStack.OpenPath($@"HKLM:\SOFTWARE\EpicGames\Unreal Engine\{engine}"))
                    {
                        if (stack.Exists)
                        {
                            var registryBasedPath = stack.Key.GetValue("InstalledDirectory") as string;
                            if (registryBasedPath != null && Directory.Exists(registryBasedPath))
                            {
                                return new EngineSpec
                                {
                                    Type = EngineSpecType.Version,
                                    Version = engine,
                                    OriginalSpec = engine,
                                    Path = registryBasedPath,
                                };
                            }
                        }
                    }
                }

                if ((flags & EngineParseFlags.WindowsFolder) != 0)
                {
                    // If the engine matches a version regex [45]\.[0-9]+(EA)?, check the Program Files folder.
                    if (_versionRegex.IsMatch(engine))
                    {
                        var candidatePath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                            "Epic Games",
                            $"UE_{engine}");
                        if (Directory.Exists(candidatePath))
                        {
                            return new EngineSpec
                            {
                                Type = EngineSpecType.Version,
                                Version = engine,
                                OriginalSpec = engine,
                                Path = candidatePath,
                            };
                        }
                    }
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                if ((flags & EngineParseFlags.MacFolder) != 0)
                {
                    // If the engine matches a version regex [45]\.[0-9]+(EA)?, check the /Users/Shared folder.
                    if (_versionRegex.IsMatch(engine))
                    {
                        var candidatePath = System.IO.Path.Combine(
                            "/Users/Shared",
                            "Epic Games",
                            $"UE_{engine}");
                        if (Directory.Exists(candidatePath))
                        {
                            return new EngineSpec
                            {
                                Type = EngineSpecType.Version,
                                Version = engine,
                                OriginalSpec = engine,
                                Path = candidatePath,
                            };
                        }
                    }
                }
            }

            if ((flags & EngineParseFlags.AbsolutePath) != 0)
            {
                // If the engine path ends in a \, remove it because it creates problems when the
                // path is passed in on the command line (usually escaping a quote " ...)
                engine = engine.TrimEnd('\\');

                // If this is an absolute path to an engine, use that.
                if (System.IO.Path.IsPathRooted(engine) &&
                    Directory.Exists(engine))
                {
                    return new EngineSpec
                    {
                        Type = EngineSpecType.Path,
                        OriginalSpec = engine,
                        Path = engine,
                    };
                }
            }

            // Could not locate engine.
            return null;
        }

        public static EngineSpec ParseEngineSpecWithoutPath(ArgumentResult result)
        {
            var engine = string.Join(" ", result.Tokens);

            var engineResult = TryParseEngine(engine);
            if (engineResult != null)
            {
                return engineResult;
            }

            result.ErrorMessage = "The specified engine could not be found. Engines can be specified as a version number like '5.2', a UEFS tag like 'uefs:...', a shared engine source like 'ses:\\\\MACHINE\\NetworkShare\\Folder' or an absolute path.";
            return null!;
        }

        public required EngineSpecType Type { get; init; }

        public required string OriginalSpec { get; init; }

        public string? Version { get; private init; }

        public string? Path { get; private init; }

        public string? UEFSPackageTag { get; private init; }

        public string? SESNetworkShare { get; private init; }

        public string? RemoteZfs { get; private init; }

        public string? GitUrl { get; private init; }

        public string? GitCommit { get; private init; }

        public NameValueCollection? GitQueryString { get; private init; }

        public string[]? FolderLayers { get; private init; }

        public string[]? ZipLayers { get; private init; }

        public string? WindowsSharedGitCachePath { get; private init; }

        public string? MacSharedGitCachePath { get; private init; }

        public override string ToString()
        {
            if (Type == EngineSpecType.Version && OriginalSpec != Path)
            {
                return $"{Version} ({Path})";
            }

            return OriginalSpec;
        }

        public BuildEngineSpecification ToBuildEngineSpecification(
            string commandName,
            DistributionSpec? distributionSpec = null,
            string? windowsSharedGitCachePath = null,
            string? macSharedGitCachePath = null)
        {
            switch (Type)
            {
                case EngineSpecType.UEFSPackageTag:
                    return BuildEngineSpecification.ForUEFSPackageTag(UEFSPackageTag!);
                case EngineSpecType.SESNetworkShare:
                    return BuildEngineSpecification.ForSESNetworkShare(SESNetworkShare!);
                case EngineSpecType.RemoteZfs:
                    return BuildEngineSpecification.ForRemoteZfs(RemoteZfs!);
                case EngineSpecType.Version:
                    return BuildEngineSpecification.ForVersionWithPath(Version!, Path!);
                case EngineSpecType.Path:
                    return BuildEngineSpecification.ForAbsolutePath(Path!);
                case EngineSpecType.GitCommit:
                    return BuildEngineSpecification.ForGitCommitWithZips(
                        GitUrl!,
                        GitCommit!,
                        ZipLayers,
                        isEngineBuild: false,
                        windowsSharedGitCachePath:
                            windowsSharedGitCachePath ?? WindowsSharedGitCachePath,
                        macSharedGitCachePath:
                            macSharedGitCachePath ?? MacSharedGitCachePath,
                        queryString: GitQueryString);
                case EngineSpecType.SelfEngineByBuildConfig:
                    if (distributionSpec != null)
                    {
                        var engineDistribution = distributionSpec!.Distribution as BuildConfigEngineDistribution;
                        if (engineDistribution!.ExternalSource != null)
                        {
                            var repositoryUrl = engineDistribution!.ExternalSource.Repository;
                            if (!repositoryUrl.Contains("://", StringComparison.Ordinal))
                            {
                                var shortSshUrlRegex = new Regex("^(.+@)*([\\w\\d\\.]+):(.*)$");
                                var shortSshUrlMatch = shortSshUrlRegex.Match(repositoryUrl);
                                if (shortSshUrlMatch.Success)
                                {
                                    repositoryUrl = $"ssh://{shortSshUrlMatch.Groups[1].Value}{shortSshUrlMatch.Groups[2].Value}/{shortSshUrlMatch.Groups[3].Value}";
                                }
                            }
                            // @note: This will round trip to ci-build as EngineSpecType.GitCommit
                            return BuildEngineSpecification.ForGitCommitWithZips(
                                repositoryUrl,
                                engineDistribution.ExternalSource.Ref,
                                engineDistribution.ExternalSource.ConsoleZips,
                                isEngineBuild: true,
                                windowsSharedGitCachePath: windowsSharedGitCachePath,
                                macSharedGitCachePath: macSharedGitCachePath);
                        }
                        else
                        {
                            return BuildEngineSpecification.ForEngineInCurrentWorkspace();
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"The EngineSpecType {Type} is not supported by the '{commandName}' command.");
                    }
                case EngineSpecType.CurrentWorkspace:
                    return BuildEngineSpecification.ForEngineInCurrentWorkspace();
                default:
                    throw new NotSupportedException($"The EngineSpecType {Type} is not supported by the '{commandName}' command.");
            }
        }
    }
}
