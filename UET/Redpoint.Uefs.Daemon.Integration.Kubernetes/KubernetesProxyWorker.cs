namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class KubernetesProxyWorker : BackgroundService
    {
        private readonly ILogger<KubernetesProxyWorker> _logger;
        private readonly KubernetesForwardingArguments _args;
        private int _secondsBackoff = 1;

        public KubernetesProxyWorker(
            ILogger<KubernetesProxyWorker> logger,
            KubernetesForwardingArguments args)
        {
            _logger = logger;
            _args = args;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_args.UnixSocketPath == null)
            {
                // We're not running the forwarder.
                return;
            }

            var goForwardPath = Path.Combine(Path.GetTempPath(), $"go-forward-{Environment.ProcessId}.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(goForwardPath)!);
            if (File.Exists(goForwardPath))
            {
                File.Delete(goForwardPath);
            }
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uefs.Daemon.Integration.Kubernetes.go-forward.exe")!)
            {
                using (var target = new FileStream(goForwardPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    await source.CopyToAsync(target, stoppingToken).ConfigureAwait(false);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                Process? process = null;
                try
                {
                    process = new Process();
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = goForwardPath,
                        ArgumentList =
                        {
                            "--port",
                            _args.Port.ToString(CultureInfo.InvariantCulture),
                            "--unix",
                            _args.UnixSocketPath
                        },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            _logger.LogInformation($"stdout: {e.Data}");
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            _logger.LogInformation($"stderr: {e.Data}");
                        }
                    };

                    _logger.LogInformation($"Starting: \"{process.StartInfo.FileName}\" {string.Join(" ", process.StartInfo.ArgumentList.Select(x => $"\"{x}\""))}");

                    if (!process.Start())
                    {
                        _logger.LogError("Failed to start go-forward!");
                    }
                    await process.WaitForExitAsync(stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    if (process != null && !process.HasExited)
                    {
                        _logger.LogInformation("Stopping go-forward...");
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                        _logger.LogInformation("go-forward has been stopped.");
                    }
                }

                stoppingToken.ThrowIfCancellationRequested();

                _logger.LogInformation($"go-forward exited with exit code {process.ExitCode}, restarting in {_secondsBackoff} seconds...");
                await Task.Delay(_secondsBackoff * 1000, stoppingToken).ConfigureAwait(false);
                _secondsBackoff *= 2;
                if (_secondsBackoff > 30)
                {
                    _secondsBackoff = 30;
                }
            }
        }
    }
}
