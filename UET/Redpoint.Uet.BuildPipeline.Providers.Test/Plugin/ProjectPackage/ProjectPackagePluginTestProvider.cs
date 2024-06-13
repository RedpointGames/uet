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
    using System.IO.Hashing;
    using System.Linq;
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
                            var execCmds = string.Join("+", (projectPackage.settings.BootTest.AutomationTests ?? [])
                                .Select(x => $"Automation RunTests {x}")
                                .Concat(["Automation Test Queue Empty"])
                                .Select(x => $"\"{x}\""));
                            var arguments = new List<string>
                            {
                                $"\"-project=$(TempPath)/{assembledProjectName}/{assembledProjectName}.uproject\"",
                                "-nop4",
                                noCodeSign,
                                $"\"-platform={projectPackage.settings.TargetPlatform}\"",
                                "-Build=local",
                                "-Test=DefaultTest",
                                "-MaxDuration=600",
                                "-Unattended",
                                $"-ExecCmds={execCmds}",
                            };
                            if (!string.IsNullOrWhiteSpace(projectPackage.settings.BootTest.DeviceId))
                            {
                                arguments.Add($"-device={projectPackage.settings.BootTest.DeviceId}");
                            }
                            if (projectPackage.settings.BootTest.ExtraGauntletArguments != null)
                            {
                                arguments.AddRange(projectPackage.settings.BootTest.ExtraGauntletArguments);
                            }

                            await writer.WriteCommandAsync(
                                new CommandElementProperties
                                {
                                    Name = "RunUnreal",
                                    Arguments = arguments,
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

            var targetPlatform = runtimeSettings["TargetPlatform"];
            var cookPlatform = runtimeSettings["CookPlatform"];
            var projectDirectory = runtimeSettings["ProjectDirectory"];
            var stagingDirectory = runtimeSettings["StagingDirectory"];

            if (targetPlatform == "Android")
            {
                // Go and sign our universal APK because the build toolchain as of 5.4 doesn't do codesigning
                // unless -distribution is specified, which prevents Gauntlet from deploying onto device.
                var buildTools = Path.Combine(
                    Environment.GetEnvironmentVariable("ANDROID_HOME")!,
                    "build-tools");
                var apksigner = Path.Combine(
                    new DirectoryInfo(buildTools).GetDirectories().First().FullName,
                    "apksigner.bat");
                var cmd = await _pathResolver.ResolveBinaryPath("cmd.exe").ConfigureAwait(false);
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

                _logger.LogInformation($"Attempting to sign APK...");
                return await _processExecutor.ExecuteAsync(
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
                        ]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }
    }
}
