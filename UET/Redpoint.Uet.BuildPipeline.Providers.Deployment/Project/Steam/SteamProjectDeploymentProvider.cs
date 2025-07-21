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
    using System.Text.RegularExpressions;
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
                                { "ProjectRoot", "$(ProjectRoot)" },
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
                if (!string.IsNullOrWhiteSpace(config.SteamUsername))
                {
                    steamUsername = config.SteamUsername;
                }
                else
                {
                    _logger.LogError("Missing STEAM_USERNAME environment variable, which is required for Steam deployments. Set this environment variable on the command line or in your build server configuration.");
                    return 1;
                }
            }
            if (string.IsNullOrWhiteSpace(steamCmdPath))
            {
                if (!string.IsNullOrWhiteSpace(config.SteamCmdPath))
                {
                    if (Path.IsPathRooted(config.SteamCmdPath))
                    {
                        steamCmdPath = config.SteamCmdPath;
                    }
                    else
                    {
                        steamCmdPath = Path.Combine(runtimeSettings["ProjectRoot"], config.SteamCmdPath);
                    }
                }
                else
                {
                    _logger.LogError("Missing STEAM_STEAMCMD_PATH environment variable, which is required for Steam deployments. This must point to the steamcmd.exe executable. Set this environment variable on the command line or in your build server configuration.");
                    return 1;
                }
            }

            _logger.LogInformation($"Using steamcmd.exe located at: {steamCmdPath}");

            var packagePath = Path.Combine(runtimeSettings["StageDirectory"], stagePlatform);

            var tempPath = Path.Combine(runtimeSettings["TempPath"], $"Steam{config.DepotID}");
            Directory.CreateDirectory(tempPath);

            await File.WriteAllTextAsync(
                Path.Combine(tempPath, "steam_selfupdate.txt"),
                $"""
                @NoPromptForPassword 1
                quit
                """,
                cancellationToken).ConfigureAwait(false);
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
                    "FileExclusion" "InstallAntiCheat.bat"
                    "FileExclusion" "UninstallAntiCheat.bat"
                    "FileExclusion" "InstallEOSServices.bat"
                    "FileExclusion" "UninstallEOSServices.bat"
                    "FileExclusion" "*/Saved/*"
                    "InstallScript" "Engine/Extras/steam_install_prereqs.vdf"
                }
                """,
                cancellationToken).ConfigureAwait(false);
            var projectName = config.Package.Target;
            var installScriptExtras = new List<string>
            {
                $$"""
                "Firewall"
                {
                    "Game Launch - {{config.DepotID}}"    "%INSTALLDIR%\\{{projectName}}.exe"
                    "Game Exe - {{config.DepotID}}"       "%INSTALLDIR%\\{{projectName}}\\Binaries\\Win64\\{{projectName}}.exe"
                }
                """
            };

            // @note: It's fine for all of the HasRunKey to be the same, because the name of the section (e.g. "Unreal Engine Prerequisites" or "EOS Services") is used as the DWORD underneath that key.

            var runProcessSections = new List<string>();
            var runProcessOnUninstallSections = new List<string>();

            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UE4PrereqSetup_x64.exe")))
            {
                runProcessSections.Add(
                    $$"""

                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UE4PrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    """);
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UnrealPrereqSetup_x64.exe")))
            {
                runProcessSections.Add(
                    $$"""

                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UnrealPrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    """
                    );
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64.exe")))
            {
                runProcessSections.Add(
                    $$"""
                    
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    """);
            }
            if (File.Exists(Path.Combine(packagePath, "Engine\\Extras\\Redist\\en-us\\vc_redist.x64.exe")))
            {
                runProcessSections.Add(
                    $$"""
                    
                        "Unreal Engine Prerequisites"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\Engine\\Extras\\Redist\\en-us\\vc_redist.x64.exe"
                            "Command 1"     "/quiet /norestart"
                            "NoCleanUp"     "1"
                        }
                    """);
            }
            if (File.Exists(Path.Combine(packagePath, "InstallEOSServices.bat")))
            {
                var installEosServicesRegex = new Regex("/install productId=([0-9a-f]+) ", RegexOptions.Multiline);
                var productIdMatch = installEosServicesRegex.Match(File.ReadAllText(Path.Combine(packagePath, "InstallEOSServices.bat")));
                if (!productIdMatch.Success)
                {
                    _logger.LogError("Unable to locate product ID in 'InstallEOSServices.bat' file!");
                    return 1;
                }
                var productId = productIdMatch.Groups[1].Value;
                _logger.LogInformation($"Using product ID '{productId}' for EOS service installation.");

                runProcessSections.Add(
                    $$"""
                    
                        "Epic Online Services"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\EpicOnlineServicesInstaller.exe"
                            "Command 1"     "/install productId={{productId}} /quiet"
                        }
                    """);
                runProcessOnUninstallSections.Add(
                    $$"""
                    
                        "Epic Online Services"
                        {
                            "Process 1"     "%INSTALLDIR%\\EpicOnlineServicesInstaller.exe"
                            "Command 1"     "/uninstall productId={{productId}} /quiet"
                        }
                    """);
            }
            if (File.Exists(Path.Combine(packagePath, "InstallAntiCheat.bat")))
            {
                var installEacServicesRegex = new Regex("install ([0-9a-f]+)\\n");
                var productIdMatch = installEacServicesRegex.Match(
                    File.ReadAllText(Path.Combine(packagePath, "InstallAntiCheat.bat"))
                        .Replace("\r", "", StringComparison.Ordinal));
                if (!productIdMatch.Success)
                {
                    _logger.LogError("Unable to locate product ID in 'InstallAntiCheat.bat' file!");
                    return 1;
                }
                var productId = productIdMatch.Groups[1].Value;
                _logger.LogInformation($"Using product ID '{productId}' for Anti-Cheat service installation.");

                runProcessSections.Add(
                    $$"""
                    
                        "Easy Anti-Cheat"
                        {
                            "HasRunKey"     "HKEY_LOCAL_MACHINE\\Software\\Valve\\Steam\\Apps\\{{config.AppID}}"
                            "Process 1"     "%INSTALLDIR%\\EasyAntiCheat\\EasyAntiCheat_EOS_Setup.exe"
                            "Command 1"     "install {{productId}}"
                        }
                    """);
                runProcessOnUninstallSections.Add(
                    $$"""
                    
                        "Easy Anti-Cheat"
                        {
                            "Process 1"     "%INSTALLDIR%\\EasyAntiCheat\\EasyAntiCheat_EOS_Setup.exe"
                            "Command 1"     "uninstall {{productId}}"
                        }
                    """);
            }

            if (runProcessSections.Count > 0)
            {
                installScriptExtras.Add(
                    $$"""

                    "Run Process"
                    {
                        {{string.Join(string.Empty, runProcessSections)}}
                    }
                    """
                );
            }
            if (runProcessOnUninstallSections.Count > 0)
            {
                installScriptExtras.Add(
                    $$"""
                    
                    "Run Process On Uninstall"
                    {
                        {{string.Join(string.Empty, runProcessOnUninstallSections)}}
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

            _logger.LogWarning($"If you see a login error below, run '{steamCmdPath}' manually and type 'login {steamUsername}' to authorize this machine.");

            _logger.LogInformation($"Making sure that steamcmd.exe is fully up-to-date...");
            {
                var shouldRetry = new RetryHolder();
                do
                {
                    shouldRetry.Retry = false;
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = steamCmdPath,
                            Arguments = [
                                "+runscript",
                                Path.Combine(tempPath, "steam_selfupdate.txt")
                            ]
                        },
                        new SteamCaptureSpecification(shouldRetry),
                        cancellationToken).ConfigureAwait(false);
                } while (shouldRetry.Retry);
            }

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

        private class SteamCaptureSpecification : ICaptureSpecification
        {
            private readonly RetryHolder _retryHolder;

            public SteamCaptureSpecification(RetryHolder retryHolder)
            {
                _retryHolder = retryHolder;
            }

            public bool InterceptStandardInput => true;

            public bool InterceptStandardOutput => true;

            public bool InterceptStandardError => false;

            public void OnReceiveStandardError(string data)
            {
            }

            public void OnReceiveStandardOutput(string data)
            {
                // If Steamcmd updated, run it again to ensure that deployment happened.
                if (data.Contains("Update complete, launching Steamcmd", StringComparison.Ordinal))
                {
                    _retryHolder.Retry = true;
                }
                Console.WriteLine(data);
            }

            public string? OnRequestStandardInputAtStartup()
            {
                return null;
            }
        }

        private class RetryHolder
        {
            public bool Retry;
        }
    }
}
