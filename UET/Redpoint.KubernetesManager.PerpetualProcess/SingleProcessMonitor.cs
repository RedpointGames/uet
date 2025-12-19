namespace Redpoint.KubernetesManager.PerpetualProcess
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Mono.Unix.Native;
    using Redpoint.KubernetesManager.Abstractions;
    using System.Diagnostics;
    using System.Globalization;

    internal class SingleProcessMonitor : IProcessMonitor, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IPathProvider? _pathProvider;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IWslDistro? _wslDistro;
        private readonly string _filename;
        private readonly string[] _arguments;
        private readonly Dictionary<string, string>? _environment;
        private readonly bool _perpetual;
        private readonly bool _silent;
        private readonly Func<CancellationToken, Task>? _beforeStart;
        private readonly Func<CancellationToken, Task>? _afterStart;
        private readonly bool _wsl;
        private Process? _currentProcess;
        private int _backOffWaitSeconds;

        public SingleProcessMonitor(
            ILogger logger,
            IPathProvider? pathProvider,
            IHostApplicationLifetime hostApplicationLifetime,
            IWslDistro? wslDistro,
            PerpetualProcessSpecification processSpecification,
            bool perpetual)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
            _wslDistro = wslDistro;
            _filename = processSpecification.Filename;
            _arguments = processSpecification.Arguments;
            _environment = processSpecification.Environment;
            _perpetual = perpetual;
            _silent = processSpecification.Silent;
            _beforeStart = processSpecification.BeforeStart;
            _afterStart = processSpecification.AfterStart;
            _wsl = processSpecification.WSL && OperatingSystem.IsWindows();
            _backOffWaitSeconds = 1;
        }

        public void Dispose()
        {
            if (_currentProcess != null)
            {
                ((IDisposable)_currentProcess).Dispose();
            }
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ProcessStartInfo startInfo;
                if (_wsl && _wslDistro != null && OperatingSystem.IsWindows())
                {
                    var distroName = await _wslDistro.GetWslDistroName(cancellationToken);
                    startInfo = new ProcessStartInfo()
                    {
                        FileName = _wslDistro.WslPath,
                    };
                    startInfo.ArgumentList.Add("-d");
                    startInfo.ArgumentList.Add(distroName);
                    startInfo.ArgumentList.Add("-u");
                    startInfo.ArgumentList.Add("root");
                    startInfo.ArgumentList.Add("-e");
                    startInfo.ArgumentList.Add(_filename);
                }
                else
                {
                    startInfo = new ProcessStartInfo()
                    {
                        FileName = _filename,
                    };
                }
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                foreach (var arg in _arguments)
                {
                    startInfo.ArgumentList.Add(arg);
                }
                if (_environment != null)
                {
                    foreach (var kv in _environment)
                    {
                        startInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                    }
                }

                var basename = Path.GetFileNameWithoutExtension(_filename);

                var semaphore = new SemaphoreSlim(0);

                StreamWriter? logFile = null;
                var lastFlush = DateTime.UtcNow;
                var logName = $"{(_wsl ? "wsl-" : string.Empty)}{basename}.{DateTime.UtcNow.Ticks}.log";
                if (!_silent && _pathProvider != null)
                {
                    Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "logs"));
                    logFile = new StreamWriter(Path.Combine(_pathProvider.RKMRoot, "logs", logName));
                }
                try
                {
                    if (!_silent)
                    {
                        if (!OperatingSystem.IsWindows() && _pathProvider != null)
                        {
                            if (File.Exists(Path.Combine(_pathProvider.RKMRoot, "logs", $"{basename}.latest.log")))
                            {
                                File.Delete(Path.Combine(_pathProvider.RKMRoot, "logs", $"{basename}.latest.log"));
                            }
                            File.CreateSymbolicLink(
                                Path.Combine(_pathProvider.RKMRoot, "logs", $"{basename}.latest.log"),
                                Path.Combine(_pathProvider.RKMRoot, "logs", logName));
                        }
                    }

                    _currentProcess = new Process();
                    _currentProcess.StartInfo = startInfo;
                    _currentProcess.EnableRaisingEvents = true;
                    _currentProcess.Exited += (sender, e) =>
                    {
                        semaphore.Release();
                    };
                    _currentProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (_silent) { return; }
                        if (e?.Data != null)
                        {
                            try
                            {
                                logFile?.WriteLine($"[{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}] {e.Data.Trim()}");
                            }
                            catch (InvalidOperationException) { }
                            // Don't emit logs during shutdown, this prevents process output
                            // from hiding things like exceptions in RKM.
                            if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                            !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested)
                            {
                                _logger.LogInformation($"{e.Data.Trim()}");
                            }
                            if (lastFlush < DateTime.UtcNow.Add(TimeSpan.FromSeconds(1)))
                            {
                                try
                                {
                                    logFile?.Flush();
                                }
                                catch (InvalidOperationException) { }
                                lastFlush = DateTime.UtcNow;
                            }
                        }
                    };
                    _currentProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (_silent) { return; }
                        if (e?.Data != null)
                        {
                            try
                            {
                                logFile?.WriteLine($"[{DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}] {e.Data.Trim()}");
                            }
                            catch (InvalidOperationException) { }
                            // Don't emit logs during shutdown, this prevents process output
                            // from hiding things like exceptions in RKM.
                            if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                                !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested)
                            {
                                _logger.LogInformation($"{e.Data.Trim()}");
                            }
                            if (lastFlush < DateTime.UtcNow.Add(TimeSpan.FromSeconds(1)))
                            {
                                try
                                {
                                    logFile?.Flush();
                                }
                                catch (InvalidOperationException) { }
                                lastFlush = DateTime.UtcNow;
                            }
                        }
                    };

                    var processType = _wsl ? "WSL " : string.Empty;

                    if (_beforeStart != null)
                    {
                        _logger.LogInformation($"(rkm) Executing 'before start' logic because this {processType}process is about to start: \"{startInfo.FileName}\" {string.Join(" ", startInfo.ArgumentList.Select(x => $"\"{x}\""))}");
                        await _beforeStart(cancellationToken);
                    }

                    if (!_silent)
                    {
                        _logger.LogInformation($"(rkm) Starting {processType}process: \"{startInfo.FileName}\" {string.Join(" ", startInfo.ArgumentList.Select(x => $"\"{x}\""))}");
                    }

                    _currentProcess.Start();
                    _currentProcess.BeginOutputReadLine();
                    _currentProcess.BeginErrorReadLine();
                    _currentProcess.StandardInput.Close();

                    if (_afterStart != null)
                    {
                        _logger.LogInformation($"(rkm) Executing 'after start' logic because this {processType}process has now been started: \"{startInfo.FileName}\" {string.Join(" ", startInfo.ArgumentList.Select(x => $"\"{x}\""))}");
                        await _afterStart(cancellationToken);
                    }

                    try
                    {
                        await semaphore.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Kill the process to ensure it doesn't stick around.
                        if (!_silent && logFile != null)
                        {
                            await logFile.WriteLineAsync($"(rkm) Terminated because RKM is exiting.");
                            await logFile.FlushAsync(cancellationToken);
                        }

                        _logger.LogInformation($"(rkm) Terminating {processType}process: {basename}");
                        if (OperatingSystem.IsWindows())
                        {
                            try
                            {
                                _currentProcess.Kill();
                            }
                            catch { }
                            _logger.LogInformation($"(rkm) Terminated {processType}process: {basename}");
                        }
                        else
                        {
                            try
                            {
                                Syscall.kill(_currentProcess.Id, Signum.SIGTERM);
                                for (var i = 0; i < 30; i++)
                                {
                                    if (!_currentProcess.HasExited)
                                    {
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                if (!_currentProcess.HasExited)
                                {
                                    _logger.LogInformation($"(rkm) Killing {processType}process: {basename}");
                                    Syscall.kill(_currentProcess.Id, Signum.SIGKILL);
                                    _logger.LogInformation($"(rkm) Killed {processType}process: {basename}");
                                }
                                else
                                {
                                    _logger.LogInformation($"(rkm) Terminated {processType}process: {basename}");
                                }
                            }
                            catch (DllNotFoundException)
                            {
                                try
                                {
                                    _currentProcess.Kill();
                                }
                                catch { }
                                _logger.LogInformation($"(rkm) Terminated {processType}process: {basename}");
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!_silent)
                    {
                        if (logFile != null)
                        {
                            await logFile.WriteLineAsync($"(rkm) Exited with exit code: {_currentProcess.ExitCode}");
                            await logFile.FlushAsync(cancellationToken);
                        }

                        if (_currentProcess.ExitCode == 0)
                        {
                            _logger.LogInformation($"(rkm) {processType}{basename} exited with exit code {_currentProcess.ExitCode}");
                        }
                        else
                        {
                            _logger.LogError($"(rkm) {processType}{basename} exited with exit code {_currentProcess.ExitCode}");
                        }
                    }

                    if (_perpetual)
                    {
                        _logger.LogInformation($"(rkm) {processType}{basename} exited, restarting it in {_backOffWaitSeconds} seconds...");
                        await Task.Delay(_backOffWaitSeconds * 1000, cancellationToken);
                        _backOffWaitSeconds *= 2;
                        if (_backOffWaitSeconds > 30)
                        {
                            _backOffWaitSeconds = 30;
                        }
                    }
                    else
                    {
                        return _currentProcess.ExitCode;
                    }
                }
                finally
                {
                    if (logFile != null)
                    {
                        logFile.Dispose();
                    }
                }
            }

            return -1;
        }
    }
}
