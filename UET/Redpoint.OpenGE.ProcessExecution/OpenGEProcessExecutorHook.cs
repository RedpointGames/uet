namespace Redpoint.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor;
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
        private readonly IOpenGEDaemon _daemon;
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        public OpenGEProcessExecutorHook(
            ILogger<OpenGEProcessExecutorHook> logger,
            IOpenGEDaemon daemon)
        {
            _logger = logger;
            _daemon = daemon;
        }

        public async Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            if (processSpecification is not OpenGEProcessSpecification)
            {
                return;
            }

            var openGEProcessSpecification = (OpenGEProcessSpecification)processSpecification;
            if (!openGEProcessSpecification.DisableOpenGE)
            {
                var xgeShimFolder = Path.Combine(Path.GetTempPath(), $"openge-shim-{Process.GetCurrentProcess().Id}");
                var xgeShimPath = Path.Combine(xgeShimFolder, "xgConsole.exe");

                await _semaphoreSlim.WaitAsync(cancellationToken);
                try
                {
                    if (!File.Exists(xgeShimPath))
                    {
                        Directory.CreateDirectory(xgeShimFolder);
                        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.OpenGE.ProcessExecution.Embedded.win_x64.xgConsole.exe"))
                        {
                            using (var target = new FileStream(xgeShimPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream!.CopyToAsync(target);
                            }
                        }
                        File.Move(xgeShimPath + ".tmp", xgeShimPath, true);
                        _logger.LogInformation("Extracted XGE shim to: " + xgeShimPath);
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }

                var path = (processSpecification.EnvironmentVariables != null && processSpecification.EnvironmentVariables.ContainsKey("PATH"))
                    ? processSpecification.EnvironmentVariables["PATH"]
                    : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                var pathComponents = path.Split(Path.PathSeparator).ToList();
                pathComponents.Add(xgeShimFolder);

                var newPath = string.Join(Path.PathSeparator, pathComponents);

                var newEnvironmentVariables = (processSpecification.EnvironmentVariables != null) ? new Dictionary<string, string>(processSpecification.EnvironmentVariables) : new Dictionary<string, string>();
                newEnvironmentVariables["PATH"] = newPath;
                newEnvironmentVariables["UET_FORCE_XGE_SHIM"] = "1";
                newEnvironmentVariables["UET_XGE_SHIM_PIPE_NAME"] = _daemon.GetConnectionString();
                processSpecification.EnvironmentVariables = newEnvironmentVariables;
            }
        }
    }
}
