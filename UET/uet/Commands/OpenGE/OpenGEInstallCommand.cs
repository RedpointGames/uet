namespace UET.Commands.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.Registry;
    using Redpoint.ServiceControl;
    using Redpoint.Uet.OpenGE;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Threading.Tasks;
    using UET.Services;

    internal class OpenGEInstallCommand
    {
        internal class Options
        {
        }

        public static Command CreateInstallCommand()
        {
            var options = new Options();
            var command = new Command("install", "Install or upgrade the OpenGE system-wide agent on this machine. This will allow you to use OpenGE from Visual Studio.");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEInstallCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private class OpenGEInstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEInstallCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly ISelfLocation _selfLocation;

            public OpenGEInstallCommandInstance(
                ILogger<OpenGEInstallCommandInstance> logger,
                IServiceControl serviceControl,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                ISelfLocation selfLocation)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _selfLocation = selfLocation;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!_serviceControl.HasPermissionToInstall)
                {
                    _logger.LogError("This command must be run as an Administrator / root.");
                    return 1;
                }


                // Get the current version.
                string version;
                string basePath;
                string uetPath;
                var currentVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (currentVersionAttribute != null && !currentVersionAttribute.InformationalVersion.EndsWith("-pre"))
                {
                    version = currentVersionAttribute.InformationalVersion;
                    basePath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            version),
                        var v when v == OperatingSystem.IsMacOS() => $"/Users/Shared/UET/{version}",
                        _ => throw new PlatformNotSupportedException()
                    };
                    var uetName = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => "uet.exe",
                        _ => "uet",
                    };
                    uetPath = Path.Combine(basePath, uetName);
                }
                else
                {
                    _logger.LogWarning("Unable to auto-detect running UET version; the xgConsole shim will be installed into the Current folder, even if the versions don't match.");
                    version = "Current";
                    basePath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            version),
                        var v when v == OperatingSystem.IsMacOS() => $"/Users/Shared/UET/{version}",
                        _ => throw new PlatformNotSupportedException()
                    };
                    uetPath = _selfLocation.GetUETLocalLocation();
                }

                // Ensure UET is installed globally.
                if (!File.Exists(uetPath))
                {
                    _logger.LogError($"Expected UET to be installed globally at '{uetPath}'. Maybe you need to run 'uet upgrade' first?");
                }

                // Extract xgConsole shim.
                var shimName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "xgConsole.exe",
                    var v when v == OperatingSystem.IsMacOS() => "xgConsole",
                    var v when v == OperatingSystem.IsLinux() => "ib_console",
                    _ => throw new PlatformNotSupportedException(),
                };
                var embeddedResourceName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "win_x64.xgConsole.exe",
                    var v when v == OperatingSystem.IsMacOS() => "osx._11._0_arm64.xgConsole",
                    var v when v == OperatingSystem.IsLinux() => "linux_x64.ib_console",
                    _ => throw new PlatformNotSupportedException(),
                };
                var xgeShimPath = Path.Combine(basePath, shimName);
                if (!File.Exists(xgeShimPath))
                {
                    var manifestName = $"{typeof(IOpenGEProvider).Namespace}.Embedded.{embeddedResourceName}";
                    var manifestStream = typeof(IOpenGEProvider).Assembly.GetManifestResourceStream(manifestName);
                    if (manifestStream == null)
                    {
                        throw new InvalidOperationException($"This process requires the OpenGE shim to be extracted, but UET was incorrectly built and doesn't have a copy of the shim as an embedded resource with the name '{manifestName}'.");
                    }
                    using (manifestStream)
                    {
                        using (var target = new FileStream(xgeShimPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await manifestStream!.CopyToAsync(target);
                        }
                    }
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        var mode = File.GetUnixFileMode(xgeShimPath + ".tmp");
                        mode |= UnixFileMode.UserExecute;
                        mode |= UnixFileMode.GroupExecute;
                        mode |= UnixFileMode.OtherExecute;
                        File.SetUnixFileMode(xgeShimPath + ".tmp", mode);
                    }
                    File.Move(xgeShimPath + ".tmp", xgeShimPath, true);
                    _logger.LogTrace("Extracted XGE shim to: " + xgeShimPath);
                }

                // Re-install OpenGE agent.
                var daemonName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "Incredibuild Agent",
                    var v when v == OperatingSystem.IsMacOS() => "openge-agent",
                    var v when v == OperatingSystem.IsLinux() => "openge-agent",
                    _ => throw new PlatformNotSupportedException(),
                };
                if (await _serviceControl.IsServiceInstalled(daemonName))
                {
                    if (await _serviceControl.IsServiceRunning(daemonName))
                    {
                        _logger.LogInformation("Stopping OpenGE agent...");
                        await _serviceControl.StopService(daemonName);
                    }

                    _logger.LogInformation("Uninstalling OpenGE agent...");
                    await _serviceControl.UninstallService(daemonName);
                }
                _logger.LogInformation("Installing OpenGE agent...");
                if (OperatingSystem.IsMacOS())
                {
                    Directory.CreateDirectory("/Users/Shared/OpenGE");
                }
                await _serviceControl.InstallService(
                    daemonName,
                    "The OpenGE agent provides remote compilation services.",
                    $@"""C:\Work\unreal-engine-tool\UET\Redpoint.OpenGE.Agent.Daemon\bin\Release\net7.0\win-x64\openge-agent.exe"" --service",
                    OperatingSystem.IsMacOS() ? "/Users/Shared/OpenGE/stdout.log" : null,
                    OperatingSystem.IsMacOS() ? "/Users/Shared/OpenGE/stderr.log" : null);

                _logger.LogInformation("Starting OpenGE agent...");
                await _serviceControl.StartService(daemonName);

                // Set 'SOFTWARE\Xoreax\IncrediBuild\BuildService' value 'CoordHost' to '127.0.0.1'.
                if (OperatingSystem.IsWindows())
                {
                    _logger.LogInformation("Set Incredibuild coordinator to 127.0.0.1 for UBT tooling...");
                    var stack = RegistryStack.OpenPath(@"HKCU:\SOFTWARE\Xoreax\IncrediBuild\BuildService", true, true);
                    stack.Key.SetValue("CoordHost", "127.0.0.1");
                }

                _logger.LogInformation("The OpenGE agent has been installed and started.");
                return 0;
                // @todo: Handle '/Command=Unused /nowait /silent' arguments in shim as UBT uses it to test if another build is running.
            }
        }
    }
}
