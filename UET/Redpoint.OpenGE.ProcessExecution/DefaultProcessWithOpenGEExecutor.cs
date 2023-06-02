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

    internal class DefaultProcessWithOpenGEExecutor : IProcessWithOpenGEExecutor
    {
        private readonly IProcessExecutor _executor;
        private readonly ILogger<DefaultProcessWithOpenGEExecutor> _logger;
        private readonly IOpenGEDaemon _daemon;
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        public DefaultProcessWithOpenGEExecutor(
            IProcessExecutor executor,
            ILogger<DefaultProcessWithOpenGEExecutor> logger,
            IOpenGEDaemon daemon)
        {
            _executor = executor;
            _logger = logger;
            _daemon = daemon;
        }

        public async Task<int> ExecuteAsync(ProcessSpecification processSpecification, ICaptureSpecification captureSpecification, CancellationToken cancellationToken)
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

            if (processSpecification.EnvironmentVariables == null)
            {
                processSpecification.EnvironmentVariables = new Dictionary<string, string>();
            }
            processSpecification.EnvironmentVariables["PATH"] = newPath;
            processSpecification.EnvironmentVariables["UET_FORCE_XGE_SHIM"] = "1";
            processSpecification.EnvironmentVariables["UET_XGE_SHIM_PIPE_NAME"] = _daemon.GetConnectionString();

            return await _executor.ExecuteAsync(
                processSpecification,
                captureSpecification,
                cancellationToken);
        }
    }
}
