namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class SteamProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentSteam>
    {
        private readonly ILogger<SteamProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public SteamProjectDeploymentProvider(
            ILogger<SteamProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "Steam";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentSteam;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentSteam)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var deployment in castedSettings)
            {
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {deployment.name}",
                        AgentType = deployment.manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {deployment.name}",
                        Requires = $"#{deployment.settings.Package.Type.ToString()}Staged_{deployment.settings.Package.Target}_{deployment.settings.Package.Platform}_{deployment.settings.Package.Configuration};$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<SteamProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentSteam>(
                            this,
                            context,
                            $"{deployment.settings.Package.Platform}.{deployment.name}".Replace(" ", ".", StringComparison.Ordinal),
                            deployment.settings,
                            new Dictionary<string, string>
                            {
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {deployment.name}",
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigProjectDeploymentSteam)configUnknown;

            var stagePlatform = config.Package.Platform;
            if (stagePlatform == "Win64")
            {
                stagePlatform = "Windows";
            }
            if (config.Package.Type == BuildConfigProjectDeploymentPackageType.Server &&
                (stagePlatform == "Windows" || stagePlatform == "Mac" || stagePlatform == "Linux"))
            {
                stagePlatform = $"{stagePlatform}Server";
            }
            if (config.Package.Type == BuildConfigProjectDeploymentPackageType.Client &&
                (stagePlatform == "Windows" || stagePlatform == "Mac" || stagePlatform == "Linux"))
            {
                stagePlatform = $"{stagePlatform}Client";
            }

            var steamUsername = Environment.GetEnvironmentVariable("STEAM_USERNAME");
            var steamCmdPath = Environment.GetEnvironmentVariable("STEAM_STEAMCMD_PATH");
            if (string.IsNullOrWhiteSpace(steamUsername))
            {
                _logger.LogError("Missing STEAM_USERNAME environment variable, which is required for Steam deployments. Set this environment variable on the command line or in your build server configuration.");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(steamCmdPath))
            {
                _logger.LogError("Missing STEAM_STEAMCMD_PATH environment variable, which is required for Steam deployments. This must point to the steamcmd.exe executable. Set this environment variable on the command line or in your build server configuration.");
                return 1;
            }

            var packagePath = Path.Combine(runtimeSettings["StageDirectory"], stagePlatform);

            var tempPath = Path.Combine(runtimeSettings["TempPath"], $"Steam{config.DepotID}");
            Directory.CreateDirectory(tempPath);

            await File.WriteAllTextAsync(
                Path.Combine(tempPath, "steam_push.txt"),
                $"""
                @NoPromptForPassword 1
                login {steamUsername}
                run_app_build {tempPath}\steam_app_{config.AppID}.vdf
                quit
                """,
                cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(tempPath, $"steam_app_{config.AppID}.vdf"),
                $$"""
                "appbuild"
                {
                	"appid" "{{config.AppID}}"
                	"desc" "Automatic deployment by UET"
                	"preview" "0"
                	"setlive" "{{config.Channel}}"
                	"buildoutput" ".\Cache\"
                	"contentroot" "{{packagePath}}"
                	"depots"
                	{
                		"{{config.DepotID}}" "steam_depot_{{config.DepotID}}.vdf"
                	}
                }
                """,
                cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(tempPath, $"steam_depot_{config.DepotID}.vdf"),
                $$"""
                "DepotBuildConfig"
                {
                    "DepotID" "{{config.DepotID}}"
                    "ContentRoot" "."
                    "FileMapping"
                    {
                        "LocalPath" "*"
                        "DepotPath" "."
                        "recursive" "1"
                    }
                    "FileExclusion" "Manifest_*.txt"
                    "FileExclusion" "*/Saved/*"
                    "InstallScript" "Engine/Extras/steam_install_prereqs.vdf"
                }
                """,
                cancellationToken).ConfigureAwait(false);
            var installScriptExtras = new List<string>
            {
                $$"""
                "Firewall"
                {
                    "Game - {{config.DepotID}}"       "%INSTALLDIR%\\$PackageTarget.exe"
                }
                """
            };
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UE4PrereqSetup_x64.exe")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\UE4PrereqSetup"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UE4PrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    }
                    """
                );
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UnrealPrereqSetup_x64.exe")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\UnrealPrereqSetup"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UnrealPrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    }
                    """
                );
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64.exe")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\UEPrereqSetup"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    }
                    """
                );
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\vc_redist.x64.exe")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\VCRedist"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\vc_redist.x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    }
                    """
                );
            }
            if (File.Exists(Path.Combine(packagePath, "InstallEOSServices.bat")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Epic Online Services"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\EOS"
                            "Process 1"     "%INSTALLDIR%\\InstallEOSServices.bat"
                        }
                    }
                    "Run Process On Uninstall"
                    {
                        "Epic Online Services"
                        {
                            "Process 1"     "%INSTALLDIR%\\UninstallEOSServices.bat"
                        }
                    }
                    """
                );
            }
            if (File.Exists(Path.Combine(packagePath, "InstallAntiCheat.bat")))
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        "Easy Anti-Cheat"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}\\AntiCheat"
                            "Process 1"     "%INSTALLDIR%\\InstallAntiCheat.bat"
                        }
                    }
                    "Run Process On Uninstall"
                    {
                        "Easy Anti-Cheat"
                        {
                            "Process 1"     "%INSTALLDIR%\\UninstallAntiCheat.bat"
                        }
                    }
                    """
                );
            }
            Directory.CreateDirectory(Path.Combine(packagePath, "Engine\\Extras"));
            await File.WriteAllTextAsync(
                Path.Combine(packagePath, "Engine\\Extras\\steam_install_prereqs.vdf"),
                $$"""
                "InstallScript"
                {
                    {{string.Join("\n", installScriptExtras)}}
                }
                """,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"Deploying to Steam using stage platform {stagePlatform} under {packagePath}...");
            return await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = steamCmdPath,
                    Arguments = [
                        "+runscript",
                        Path.Combine(tempPath, "steam_push.txt")
                    ]
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
