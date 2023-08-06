namespace Redpoint.Uet.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    internal class OpenGEProcessExecutorHook : IProcessExecutorHook
    {
        private readonly ILogger<OpenGEProcessExecutorHook> _logger;
        private readonly IOpenGEProvider _provider;
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        public OpenGEProcessExecutorHook(
            ILogger<OpenGEProcessExecutorHook> logger,
            IOpenGEProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        public async Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            if (processSpecification is not OpenGEProcessSpecification openGEProcessSpecification)
            {
                return;
            }

            var environmentInfo = await _provider.GetOpenGEEnvironmentInfo();

            if (!environmentInfo.ShouldUseOpenGE || openGEProcessSpecification.DisableOpenGE)
            {
                return;
            }

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

            var xgeShimFolder = Path.Combine(Path.GetTempPath(), $"openge-shim-{Process.GetCurrentProcess().Id}");
            var xgeShimPath = Path.Combine(xgeShimFolder, shimName);

            await _semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(xgeShimPath))
                {
                    Directory.CreateDirectory(xgeShimFolder);
                    var manifestName = $"{typeof(OpenGEProcessExecutorHook).Namespace}.Embedded.{embeddedResourceName}";
                    var manifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestName);
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
                    _logger.LogInformation("Extracted XGE shim to: " + xgeShimPath);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            var path = processSpecification.EnvironmentVariables != null && processSpecification.EnvironmentVariables.ContainsKey("PATH")
                ? processSpecification.EnvironmentVariables["PATH"]
                : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            var pathComponents = path.Split(Path.PathSeparator).ToList();
            pathComponents.Add(xgeShimFolder);

            var newPath = string.Join(Path.PathSeparator, pathComponents);

            var newEnvironmentVariables = processSpecification.EnvironmentVariables != null ? new Dictionary<string, string>(processSpecification.EnvironmentVariables) : new Dictionary<string, string>();
            newEnvironmentVariables["PATH"] = newPath;
            newEnvironmentVariables["UET_FORCE_XGE_SHIM"] = "1";
            if (!environmentInfo.UsingSystemWideDaemon)
            {
                newEnvironmentVariables["UET_XGE_SHIM_PIPE_NAME"] = environmentInfo.PerProcessDispatcherPipeName;
            }
            processSpecification.EnvironmentVariables = newEnvironmentVariables;

            // @note: Handle a special case where UET wants to launch the
            // OpenGE shim directly, rather than running a process that wants
            // to find the OpenGE shim on the PATH.
            if (processSpecification.FilePath == "__openge__")
            {
                processSpecification.FilePath = xgeShimPath;
            }
        }
    }
}
