namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.IO.Hashing;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using static System.Net.Mime.MediaTypeNames;

    internal sealed class ProjectPackagePluginTestProvider : IPluginTestProvider, IDynamicReentrantExecutor<BuildConfigPluginDistribution, BuildConfigPluginTestProjectPackage>
    {
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;
        private readonly IPathResolver _pathResolver;
        private readonly ILogger<ProjectPackagePluginTestProvider> _logger;
        private readonly IProcessExecutor _processExecutor;

        public ProjectPackagePluginTestProvider(
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider,
            IPathResolver pathResolver,
            ILogger<ProjectPackagePluginTestProvider> logger,
            IProcessExecutor processExecutor)
        {
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
            _pathResolver = pathResolver;
            _logger = logger;
            _processExecutor = processExecutor;
        }

        public string Type => "ProjectPackage";

        public IRuntimeJson DynamicSettings { get; } = new TestProviderRuntimeJson(TestProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginTestProjectPackage;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> elements)
        {
            var castedSettings = elements
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestProjectPackage)x.DynamicSettings))
                .ToList();

            // Ensure we have the test project available.
            await _pluginTestProjectEmitProvider.EnsureTestProjectNodesArePresentAsync(
                context,
                writer).ConfigureAwait(false);

            foreach (var projectPackage in castedSettings)
            {
                // Figure out what binaries we need to make the plugin to run the test.
                var inputBinaries = new List<string>
                {
                    $"#EditorBinaries_{projectPackage.settings.HostPlatform}",
                    $"#GameBinaries_$(EnginePrefix)Game_{projectPackage.settings.TargetPlatform}_Development",
                };

                // Hash the name so we can make safe tags.
                var uniqueHash = BitConverter.ToUInt64(XxHash64.Hash(Encoding.UTF8.GetBytes($"ProjectPackage_{projectPackage.name}")));
                var assembledProjectName = $"Project{uniqueHash}";

                // Create the package for the test project, where the package only depends on the relevant platforms.
                await writer.WriteExpandAsync(
                    new ExpandElementProperties
                    {
                        Name = "Create Package",
                        Attributes =
                        {
                            { "AgentStage", "Assemble Test Projects" },
                            { "AgentType", projectPackage.settings.HostPlatform.ToString() },
                            { "NodeName", $"Assemble Plugin {projectPackage.name}" },
                            { "InputProject", $"#HostProject" },
                            { "InputBaseDir", $"$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)" },
                            { "InputBinaries", string.Join(';', inputBinaries) },
                            { "OutputDir", $"$(TempPath)/ProjectPackage_{uniqueHash}_Plugin" },
                            { "OutputTag", $"#ProjectPackage_{uniqueHash}_Plugin" },
                        }
                    }).ConfigureAwait(false);

                // Create the test project that uses that generated plugin package.
                await writer.WriteExpandAsync(
                    new ExpandElementProperties
                    {
                        Name = "Assemble Test Project",
                        Attributes =
                        {
                            { "AgentStage", "Assemble Test Projects" },
                            { "AgentType", projectPackage.settings.HostPlatform.ToString() },
                            { "NodeName", $"Assemble Project {projectPackage.name}" },
                            { "AssembledProjectName", assembledProjectName },
                            { "InputFiles", $"#ProjectPackage_{uniqueHash}_Plugin" },
                            { "InputPackageDir", $"$(TempPath)/ProjectPackage_{uniqueHash}_Plugin" },
                            { "OutputTag", $"#ProjectPackage_{uniqueHash}_Project" },
                            { "IsForGauntlet", projectPackage.settings.BootTest != null ? "true" : "false" },
                            { "InputProjectFilesPath", projectPackage.settings.ProjectCopyFilesPath ?? string.Empty },
                        }
                    }).ConfigureAwait(false);

                // If the project requires compilation, emit node for building the editor and target platform. This
                // is only necessary if ProjectCopyFilesPath copies source code files into the project.
                var additionalCookDependencies = new List<string>();
                var additionalPackageDependencies = new List<string>();
                var editorTarget = $"{assembledProjectName}Editor";
                var gameTarget = assembledProjectName;

                // Build the editor.
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Build Test Project",
                        AgentType = projectPackage.settings.HostPlatform.ToString(),
                        NodeName = $"Build Editor {projectPackage.name} {projectPackage.settings.HostPlatform}",
                        Requires = $"#ProjectPackage_{uniqueHash}_Project",
                        Produces = $"#ProjectPackage_{uniqueHash}_EditorBinaries",
                    },
                    async writer =>
                    {
                        await writer.WriteExpandAsync(
                            new ExpandElementProperties
                            {
                                Name = "RemoveStalePrecompiledHeaders",
                                Attributes =
                                {
                                    { "ProjectPath", $"$(TempPath)/Project{uniqueHash}/" },
                                    { "TargetName", editorTarget },
                                    { "TargetPlatform", projectPackage.settings.HostPlatform.ToString() },
                                    { "TargetConfiguration", $"Development" },
                                }
                            }).ConfigureAwait(false);
                        await writer.WriteCompileAsync(
                            new CompileElementProperties
                            {
                                Target = editorTarget,
                                Platform = projectPackage.settings.HostPlatform.ToString(),
                                Configuration = "Development",
                                Tag = $"#ProjectPackage_{uniqueHash}_EditorBinaries",
                                Arguments =
                                [
                                    $"-Project=\"$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject\"",
                                    "$(AdditionalArguments)",
                                ]
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                additionalCookDependencies.Add($"#ProjectPackage_{uniqueHash}_EditorBinaries");
                additionalPackageDependencies.Add($"#ProjectPackage_{uniqueHash}_EditorBinaries");

                // Build the target platform.
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Build Test Project",
                        AgentType = projectPackage.settings.HostPlatform.ToString(),
                        NodeName = $"Build Game {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                        Requires = $"#ProjectPackage_{uniqueHash}_Project",
                        Produces = $"#ProjectPackage_{uniqueHash}_GameBinaries",
                    },
                    async writer =>
                    {
                        await writer.WriteExpandAsync(
                            new ExpandElementProperties
                            {
                                Name = "RemoveStalePrecompiledHeaders",
                                Attributes =
                                {
                                    { "ProjectPath", $"$(TempPath)/Project{uniqueHash}/" },
                                    { "TargetName", gameTarget },
                                    { "TargetPlatform", projectPackage.settings.TargetPlatform.ToString() },
                                    { "TargetConfiguration", $"Development" },
                                }
                            }).ConfigureAwait(false);
                        await writer.WriteCompileAsync(
                            new CompileElementProperties
                            {
                                Target = gameTarget,
                                Platform = projectPackage.settings.TargetPlatform.ToString(),
                                Configuration = "Development",
                                Tag = $"#ProjectPackage_{uniqueHash}_GameBinaries",
                                Arguments =
                                [
                                    $"-Project=\"$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject\"",
                                    "$(AdditionalArguments)",
                                ]
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                additionalPackageDependencies.Add($"#ProjectPackage_{uniqueHash}_GameBinaries");

                // Cook the test project.
                var cookPlatform = projectPackage.settings.TargetPlatform.ToString();
                if (cookPlatform == "Win64")
                {
                    cookPlatform = "Windows";
                }
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Cook Test Project",
                        AgentType = projectPackage.settings.HostPlatform.ToString(),
                        NodeName = $"Cook {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                        Requires = string.Join(';', new[] { $"#ProjectPackage_{uniqueHash}_Project;" }.Concat(additionalCookDependencies)),
                        Produces = $"#ProjectPackage_{uniqueHash}_CookedContent",
                    },
                    async writer =>
                    {
                        await writer.WriteCookAsync(
                            new CookElementProperties
                            {
                                Project = $"$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject",
                                Platform = cookPlatform,
                                Tag = $"#ProjectPackage_{uniqueHash}_CookedContent"
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                additionalPackageDependencies.Add($"#ProjectPackage_{uniqueHash}_CookedContent");

                // Package and stage the test project.
                var noCodeSign =
                    (string.Equals(projectPackage.settings.TargetPlatform, "Win64", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(projectPackage.settings.TargetPlatform, "Mac", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(projectPackage.settings.TargetPlatform, "Linux", StringComparison.OrdinalIgnoreCase))
                    ? "-NoCodeSign" : string.Empty;
                var isMobile = string.Equals(projectPackage.settings.TargetPlatform, "Android", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(projectPackage.settings.TargetPlatform, "IOS", StringComparison.OrdinalIgnoreCase);
                var packageFlag = isMobile ? "-package" : string.Empty;
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Package Test Project",
                        AgentType = projectPackage.settings.HostPlatform.ToString(),
                        NodeName = $"Package {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                        Requires = string.Join(';', new[] { $"#ProjectPackage_{uniqueHash}_Project;" }.Concat(additionalPackageDependencies)),
                        Produces = $"#ProjectPackage_{uniqueHash}_Staged",
                    },
                    async writer =>
                    {
                        await writer.WriteSpawnAsync(
                            new SpawnElementProperties
                            {
                                Exe = "$(UETPath)",
                                Arguments =
                                [
                                    "$(UETGlobalArgs)",
                                    "internal",
                                    "reparent-additional-properties-in-targets",
                                    "--project-directory-path",
                                    $"\"$(TempPath)/{assembledProjectName}\"",
                                ]
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicReentrantSpawnAsync<ProjectPackagePluginTestProvider, BuildConfigPluginDistribution, BuildConfigPluginTestProjectPackage>(
                            this,
                            context,
                            $"{projectPackage.settings.TargetPlatform}.{projectPackage.name}".Replace(" ", ".", StringComparison.Ordinal),
                            projectPackage.settings,
                            new Dictionary<string, string>
                            {
                                { "Stage", "PrePackage" },
                                { "TargetPlatform", projectPackage.settings.TargetPlatform },
                                { "CookPlatform", cookPlatform },
                                { "ProjectDirectory", $"$(TempPath)/{assembledProjectName}" },
                                { "StagingDirectory", $"$(TempPath)/{assembledProjectName}/Saved/StagedBuilds" },
                            }).ConfigureAwait(false);
                        await writer.WriteCommandAsync(
                            new CommandElementProperties
                            {
                                Name = "BuildCookRun",
                                Arguments =
                                [
                                    $"\"-project=$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject\"",
                                    "-nop4",
                                    noCodeSign,
                                    $"\"-platform={projectPackage.settings.TargetPlatform}\"",
                                    "\"-clientconfig=Development\"",
                                    "-SkipCook",
                                    "-cook",
                                    "-pak",
                                    packageFlag,
                                    "-stage",
                                    $"\"-stagingdirectory=$(TempPath)/{assembledProjectName}/Saved/StagedBuilds\"",
                                    "-unattended",
                                    "-stdlog",
                                ]
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicReentrantSpawnAsync<ProjectPackagePluginTestProvider, BuildConfigPluginDistribution, BuildConfigPluginTestProjectPackage>(
                            this,
                            context,
                            $"{projectPackage.settings.TargetPlatform}.{projectPackage.name}".Replace(" ", ".", StringComparison.Ordinal),
                            projectPackage.settings,
                            new Dictionary<string, string>
                            {
                                { "Stage", "PostPackage" },
                                { "TargetPlatform", projectPackage.settings.TargetPlatform },
                                { "CookPlatform", cookPlatform },
                                { "ProjectDirectory", $"$(TempPath)/{assembledProjectName}" },
                                { "StagingDirectory", $"$(TempPath)/{assembledProjectName}/Saved/StagedBuilds" },
                            }).ConfigureAwait(false);
                        await writer.WriteTagAsync(
                            new TagElementProperties
                            {
                                BaseDir = $"$(TempPath)/{assembledProjectName}/Saved/StagedBuilds/{cookPlatform}",
                                Files = "...",
                                With = $"#ProjectPackage_{uniqueHash}_Staged",
                            }).ConfigureAwait(false);
                        if (isMobile)
                        {
                            await writer.WriteTagAsync(
                                new TagElementProperties
                                {
                                    BaseDir = $"$(TempPath)/{assembledProjectName}/Binaries/{projectPackage.settings.TargetPlatform}",
                                    Files = "...",
                                    With = $"#ProjectPackage_{uniqueHash}_Staged",
                                }).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);

                // If we have a boot test configured, run it.
                if (projectPackage.settings.BootTest != null)
                {
                    // Run Gauntlet to deploy onto device.
                    await writer.WriteAgentNodeAsync(
                        new AgentNodeElementProperties
                        {
                            AgentStage = $"Boot Test Project",
                            AgentType = projectPackage.settings.HostPlatform.ToString() + (string.IsNullOrWhiteSpace(projectPackage.settings.BootTest.BuildMachineTag) ? string.Empty : $" Tag-{projectPackage.settings.BootTest.BuildMachineTag}"),
                            NodeName = $"Boot Test {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                            Requires = string.Join(';', new[] { $"#ProjectPackage_{uniqueHash}_Project;", $"#ProjectPackage_{uniqueHash}_Staged" }.Concat(additionalPackageDependencies)),
                        },
                        async writer =>
                        {
                            var arguments = new List<string>
                            {
                                $"\"-project=$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject\"",
                                "-nop4",
                                noCodeSign,
                                $"\"-platform={projectPackage.settings.TargetPlatform}\"",
                                "-Build=local",
                            };
                            if (!string.IsNullOrWhiteSpace(projectPackage.settings.BootTest.DeviceId))
                            {
                                arguments.Add($"-device={projectPackage.settings.TargetPlatform}:{projectPackage.settings.BootTest.DeviceId}");
                            }
                            if (projectPackage.settings.BootTest.GauntletArguments != null)
                            {
                                arguments.AddRange(projectPackage.settings.BootTest.GauntletArguments);
                            }

                            await writer.WriteDynamicReentrantSpawnAsync<ProjectPackagePluginTestProvider, BuildConfigPluginDistribution, BuildConfigPluginTestProjectPackage>(
                                this,
                                context,
                                $"{projectPackage.settings.TargetPlatform}.{projectPackage.name}".Replace(" ", ".", StringComparison.Ordinal),
                                projectPackage.settings,
                                new Dictionary<string, string>
                                {
                                    { "Stage", "PreGauntlet" },
                                    { "TargetPlatform", projectPackage.settings.TargetPlatform },
                                    { "CookPlatform", cookPlatform },
                                    { "ProjectDirectory", $"$(TempPath)/{assembledProjectName}" },
                                    { "StagingDirectory", $"$(TempPath)/{assembledProjectName}/Saved/StagedBuilds" },
                                    { "DeviceId", projectPackage.settings.BootTest.DeviceId ?? string.Empty },
                                    { "EnginePath", "$(EnginePath)" },
                                }).ConfigureAwait(false);
                            await writer.WriteCommandAsync(
                                new CommandElementProperties
                                {
                                    Name = "RunUnreal",
                                    Arguments = arguments,
                                }).ConfigureAwait(false);
                            await writer.WriteDynamicReentrantSpawnAsync<ProjectPackagePluginTestProvider, BuildConfigPluginDistribution, BuildConfigPluginTestProjectPackage>(
                                this,
                                context,
                                $"{projectPackage.settings.TargetPlatform}.{projectPackage.name}".Replace(" ", ".", StringComparison.Ordinal),
                                projectPackage.settings,
                                new Dictionary<string, string>
                                {
                                    { "Stage", "PostGauntlet" },
                                    { "TargetPlatform", projectPackage.settings.TargetPlatform },
                                    { "CookPlatform", cookPlatform },
                                    { "ProjectDirectory", $"$(TempPath)/{assembledProjectName}" },
                                    { "StagingDirectory", $"$(TempPath)/{assembledProjectName}/Saved/StagedBuilds" },
                                    { "DeviceId", projectPackage.settings.BootTest.DeviceId ?? string.Empty },
                                    { "EnginePath", "$(EnginePath)" },
                                }).ConfigureAwait(false);
                        }).ConfigureAwait(false);

                    // Make sure we depend on the boot test passing.
                    await writer.WriteDynamicNodeAppendAsync(
                        new DynamicNodeAppendElementProperties
                        {
                            NodeName = $"Boot Test {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                            MustPassForLaterDeployment = true,
                        }).ConfigureAwait(false);
                }
                else
                {
                    // Make sure we depend on the packaging passing.
                    await writer.WriteDynamicNodeAppendAsync(
                        new DynamicNodeAppendElementProperties
                        {
                            NodeName = $"Package {projectPackage.name} {projectPackage.settings.TargetPlatform}",
                            MustPassForLaterDeployment = true,
                        }).ConfigureAwait(false);
                }
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigPluginTestProjectPackage)configUnknown;

            var stage = runtimeSettings["Stage"];
            var targetPlatform = runtimeSettings["TargetPlatform"];
            var cookPlatform = runtimeSettings["CookPlatform"];
            var projectDirectory = runtimeSettings["ProjectDirectory"];
            var stagingDirectory = runtimeSettings["StagingDirectory"];

            if (stage == "PrePackage" && targetPlatform == "IOS")
            {
                // We have to create Intermediate/ProjectFilesIOS so that Info.Template.plist gets generated
                // correctly.
                Directory.CreateDirectory(Path.Combine(projectDirectory, "Intermediate", "ProjectFilesIOS"));
                return 0;
            }

            if (stage == "PostPackage" && targetPlatform == "Android")
            {
                // Go and sign our universal APK because the build toolchain as of 5.4 doesn't do codesigning
                // unless -distribution is specified, which prevents Gauntlet from deploying onto device.
                var buildTools = Path.Combine(
                    Environment.GetEnvironmentVariable("ANDROID_HOME")!,
                    "build-tools");
                var apksigner = Path.Combine(
                    new DirectoryInfo(buildTools).GetDirectories().First().FullName,
                    "apksigner.bat");
                var cmd = await _pathResolver.ResolveBinaryPath("cmd").ConfigureAwait(false);
                _logger.LogInformation($"Path to apksigner.bat: {apksigner}");
                _logger.LogInformation($"Path to cmd.exe: {cmd}");
                _logger.LogInformation($"Project directory: {projectDirectory}");
                _logger.LogInformation($"Staging directory: {stagingDirectory}");

                // Do a real basic scan for code signing settings, just enough to get this working.
                var configPath = Path.Combine(projectDirectory, "Config", "DefaultEngine.ini");
                string keystoreName = string.Empty, keystoreAlias = string.Empty, keystorePassword = string.Empty, keyPassword = string.Empty;
                using (var reader = new StreamReader(configPath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false))!.Trim();
                        if (line.StartsWith("KeyStore=", StringComparison.Ordinal))
                        {
                            keystoreName = line.Substring("KeyStore=".Length);
                        }
                        else if (line.StartsWith("KeyAlias=", StringComparison.Ordinal))
                        {
                            keystoreAlias = line.Substring("KeyAlias=".Length);
                        }
                        else if (line.StartsWith("KeyStorePassword=", StringComparison.Ordinal))
                        {
                            keystorePassword = line.Substring("KeyStorePassword=".Length);
                        }
                        else if (line.StartsWith("KeyPassword=", StringComparison.Ordinal))
                        {
                            keyPassword = line.Substring("KeyPassword=".Length);
                        }
                    }
                }
                var keystorePath = Path.Combine(projectDirectory, "Build", "Android", keystoreName);
                _logger.LogInformation($"Key store name: {keystoreName}");
                _logger.LogInformation($"Key store path: {keystorePath}");
                _logger.LogInformation($"Key store alias: {keystoreAlias}");
                _logger.LogInformation($"Key store password: {keystorePassword}");
                _logger.LogInformation($"Key password: {keyPassword}");

                var apkFolder = Path.Combine(stagingDirectory, "..", "..", "Binaries", "Android");
                var apks = new DirectoryInfo(apkFolder).GetFiles("*.apk");
                if (apks.Length == 0)
                {
                    _logger.LogError($"No APKs located in folder: {apkFolder}");
                    return 1;
                }
                foreach (var apk in apks)
                {
                    _logger.LogInformation($"Signing APK: {apk.FullName}");
                    var apkExitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = cmd,
                            Arguments = [
                                "/C",
                                apksigner,
                                "sign",
                                "--ks",
                                keystorePath,
                                "--ks-key-alias",
                                keystoreAlias,
                                "--ks-pass",
                                $"pass:{keystorePassword}",
                                string.IsNullOrWhiteSpace(keyPassword) ? string.Empty : "--key-pass",
                                string.IsNullOrWhiteSpace(keyPassword) ? string.Empty : $"pass:{keyPassword}",
                                apk.FullName,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                    if (apkExitCode != 0)
                    {
                        _logger.LogError($"Failed to sign APK: {apk.FullName}");
                        return apkExitCode;
                    }
                }
            }

            if (stage == "PostPackage" && targetPlatform == "IOS")
            {
                // We have to generate the .ipa since Gauntlet requires it and it doesn't
                // get created in modern Xcode packaging.

                var appFolder = Path.Combine(stagingDirectory, "..", "..", "Binaries", "IOS");
                var apps = new DirectoryInfo(appFolder).GetDirectories("*.app");
                if (apps.Length == 0)
                {
                    _logger.LogError($"No .app directory located in folder: {appFolder}");
                    return 1;
                }
                foreach (var app in apps)
                {
                    var payloadPath = app.FullName + ".payload";
                    var payloadSubdirPath = Path.Combine(
                        payloadPath,
                        "Payload",
                        app.Name);
                    var ipaPath = Path.Combine(
                        app.Parent!.FullName,
                        Path.GetFileNameWithoutExtension(app.Name) + ".ipa");
                    if (File.Exists(ipaPath))
                    {
                        File.Delete(ipaPath);
                    }

                    _logger.LogInformation($"Zipping .app to IPA: {app}");
                    _logger.LogInformation($"  .payload temporary path: {payloadPath}");
                    _logger.LogInformation($"  .payload path: {payloadSubdirPath}");
                    _logger.LogInformation($"  .ipa path: {ipaPath}");

                    if (Directory.Exists(payloadPath))
                    {
                        Directory.Delete(payloadPath, true);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(payloadSubdirPath)!);
                    Directory.Move(app.FullName, payloadSubdirPath);
                    ZipFile.CreateFromDirectory(payloadPath, ipaPath);

                    using (var stream = new FileStream(ipaPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                        {
                            _logger.LogInformation($"  First entry in ZIP archive: {archive.Entries.First().FullName}");
                        }
                    }
                }

                return 0;
            }

            if (stage == "PreGauntlet" && targetPlatform == "IOS")
            {
                var enginePath = runtimeSettings["EnginePath"];

                var iosDeployFolderPath = Path.Combine(enginePath, "Engine", "Extras", "ThirdPartyNotUE", "ios-deploy", "bin");
                var iosDeployFilePath = Path.Combine(iosDeployFolderPath, "ios-deploy");

                _logger.LogInformation($"ios-deploy folder: {iosDeployFolderPath}");
                _logger.LogInformation($"ios-deploy file: {iosDeployFilePath}");

                // The launcher distribution of Unreal Engine for macOS is broken and doesn't
                // ship with the ios-deploy binary, which is necessary for Gauntlet to work.
                Directory.CreateDirectory(iosDeployFolderPath);
                if (!File.Exists(iosDeployFilePath))
                {
                    _logger.LogInformation($"Extracting ios-deploy tool...");
                    using (var writer = new FileStream(iosDeployFilePath + ".tmp", FileMode.Create, FileAccess.Write))
                    {
                        using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage.ios-deploy"))
                        {
                            await reader!.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(
                            iosDeployFilePath + ".tmp",
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
                    }
                    File.Move(iosDeployFilePath + ".tmp", iosDeployFilePath);
                    _logger.LogInformation($"Extracted ios-deploy tool.");
                }
            }

            if (stage == "PreGauntlet" && targetPlatform == "Android")
            {
                var deviceId = runtimeSettings["DeviceId"];

                var adbFilePath = Path.Combine(
                    Environment.GetEnvironmentVariable("ANDROID_HOME")!,
                    "platform-tools",
                    "adb.exe");

                if (deviceId.Contains("._tcp.", StringComparison.Ordinal))
                {
                    // Pre-connect via mDNS. This is necessary because if we're not already connected to the device when Gauntlet runs, it will append :5555 to the address, which we don't want for mDNS.
                    _logger.LogInformation($"Checking mDNS service is running...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = adbFilePath,
                            Arguments = ["mdns", "check"],
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Reporting what mDNS services can be discovered for diagnostic purposes:");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = adbFilePath,
                            Arguments = ["mdns", "services"],
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Connecting to Android device '{deviceId}' via mDNS before Gauntlet starts...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = adbFilePath,
                            Arguments = ["connect", deviceId],
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Checking that we're connected to Android device '{deviceId}' via mDNS and authorized...");
                    var devicesStringBuilder = new StringBuilder();
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = adbFilePath,
                            Arguments = ["devices"],
                        },
                        CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(devicesStringBuilder),
                        cancellationToken).ConfigureAwait(false);

                    var found = false;
                    var devicesList = devicesStringBuilder.ToString()
                        .Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var deviceEntry in devicesList)
                    {
                        if (deviceEntry.StartsWith(deviceId, StringComparison.Ordinal) &&
                            deviceEntry.EndsWith("device", StringComparison.Ordinal))
                        {
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        _logger.LogError($"Did not detect authorized connection to '{deviceId}' with 'adb devices':");
                        _logger.LogError(devicesStringBuilder.ToString());
                        return 1;
                    }

                    _logger.LogInformation($"Successfully connected to Android device '{deviceId} over mDNS!");
                }

                {
                    var adbArguments = new List<LogicalProcessArgument>();
                    var deviceIdDescriptor = "(default)";
                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        adbArguments.Add("-s");
                        adbArguments.Add(deviceId);
                        deviceIdDescriptor = deviceId;
                    }

                    adbArguments.AddRange([
                        "shell",
                        "-n",
                        "am",
                        "broadcast",
                        "-a",
                        "com.oculus.vrpowermanager.prox_close"
                    ]);

                    _logger.LogInformation($"Turning off Quest proximity sensor for device: {deviceIdDescriptor}. This is expected to gracefully fail if the device is not a Meta Quest device.");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = adbFilePath,
                            Arguments = adbArguments,
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return 0;
        }
    }
}
