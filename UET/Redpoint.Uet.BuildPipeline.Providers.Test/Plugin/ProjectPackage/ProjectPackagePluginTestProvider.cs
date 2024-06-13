namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage
{
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

    internal sealed class ProjectPackagePluginTestProvider : IPluginTestProvider
    {
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;

        public ProjectPackagePluginTestProvider(
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider)
        {
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
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
                var editorTarget = "$(AssembledProjectName)Editor";
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
                                $"\"-platform={cookPlatform}\"",
                                "-Build=local",
                                "-Test=DefaultTest",
                                "-MaxDuration=600",
                                "-Unattended",
                                "-cleandevice",
                                $"-ExecCmds={execCmds}",
                            };
                            if (!string.IsNullOrWhiteSpace(projectPackage.settings.BootTest.DeviceId))
                            {
                                arguments.Add($"-device={projectPackage.settings.BootTest.DeviceId}");
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
    }
}
