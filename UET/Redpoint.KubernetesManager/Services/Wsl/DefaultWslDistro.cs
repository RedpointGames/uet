namespace Redpoint.KubernetesManager.Services.Windows
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services;
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;

    internal class DefaultWslDistro : IWslDistro, IDisposable
    {
        private readonly ILogger<DefaultWslDistro> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IAssetManager _assetManager;
        private readonly IPathProvider _pathProvider;
        private readonly SemaphoreSlim _semaphore;
        private bool _installed;
        private readonly string _distroName;

        public DefaultWslDistro(
            ILogger<DefaultWslDistro> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IAssetManager assetManager,
            IPathProvider pathProvider)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _assetManager = assetManager;
            _pathProvider = pathProvider;
            _semaphore = new SemaphoreSlim(1);
            _installed = false;
            _distroName = $"RKM-Kubernetes-{_pathProvider.RKMInstallationId}";
        }

        [SupportedOSPlatform("windows")]
        public string WslPath => Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "wsl.exe");

        [SupportedOSPlatform("windows")]
        public async Task<int> RunWslInvocation(string[] args, string input, Encoding encoding, CancellationToken cancellationToken, string? filename = null)
        {
            // Since IProcessMonitor uses IWslDistro, we have to do this manually.
            var startInfo = new ProcessStartInfo
            {
                FileName = filename ?? WslPath,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = encoding,
                StandardErrorEncoding = encoding,
                StandardOutputEncoding = encoding,
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var semaphore = new SemaphoreSlim(0);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var currentProcess = new Process();
            currentProcess.StartInfo = startInfo;
            currentProcess.EnableRaisingEvents = true;
            currentProcess.Exited += (sender, e) =>
            {
                semaphore.Release();
            };
            currentProcess.Start();
            currentProcess.StandardInput.Write(input);
            currentProcess.StandardInput.Close();
            currentProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args?.Data))
                {
                    _logger.LogInformation($"stdout: {args?.Data}");
                }
            };
            currentProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args?.Data))
                {
                    _logger.LogInformation($"stderr: {args?.Data}");
                }
            };
            currentProcess.BeginOutputReadLine();
            currentProcess.BeginErrorReadLine();
            await semaphore.WaitAsync(cancellationToken);
            await currentProcess.WaitForExitAsync(cancellationToken);
            return currentProcess.ExitCode;
        }

        [SupportedOSPlatform("windows")]
        public async Task<string> CaptureWslInvocation(string[] args, Encoding encoding, CancellationToken cancellationToken, string? filename = null)
        {
            // Since IProcessMonitor uses IWslDistro, we have to do this manually.
            var startInfo = new ProcessStartInfo
            {
                FileName = filename ?? WslPath,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardInputEncoding = encoding,
                StandardErrorEncoding = encoding,
                StandardOutputEncoding = encoding,
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var semaphore = new SemaphoreSlim(0);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var currentProcess = new Process();
            currentProcess.StartInfo = startInfo;
            currentProcess.EnableRaisingEvents = true;
            currentProcess.Exited += (sender, e) =>
            {
                semaphore.Release();
            };
            currentProcess.OutputDataReceived += (sender, e) =>
            {
                if (e?.Data != null)
                {
                    stdout.AppendLine(e?.Data.Trim());
                }
            };
            currentProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e?.Data != null)
                {
                    stderr.AppendLine(e?.Data.Trim());
                }
            };
            currentProcess.Start();
            currentProcess.BeginOutputReadLine();
            currentProcess.BeginErrorReadLine();
            currentProcess.StandardInput.Close();
            await semaphore.WaitAsync(cancellationToken);
            await currentProcess.WaitForExitAsync(cancellationToken);
            return stdout.ToString();
        }

        [SupportedOSPlatform("windows")]
        private async Task<bool> EnsureInstalled(CancellationToken cancellationToken)
        {
            if (_installed)
            {
                return true;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_installed)
                {
                    return true;
                }

                if (File.Exists(Path.Combine(_pathProvider.RKMRoot, "ubuntu-wsl", "ext4.vhdx")))
                {
                    _installed = true;
                    return true;
                }

                _logger.LogInformation("One or more processes require WSL and it's not installed, installing WSL now...");
                await _assetManager.EnsureAsset("RKM:Downloads:UbuntuWSL:Windows", "ubuntu-package.zip", cancellationToken);
                await _assetManager.ExtractAsset("ubuntu-package.zip", Path.Combine(_pathProvider.RKMRoot, "ubuntu-package"), cancellationToken);
                if (!File.Exists(Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion, "ubuntu-wsl.zip")))
                {
                    var x64File = Directory.GetFiles(Path.Combine(_pathProvider.RKMRoot, "ubuntu-package")).FirstOrDefault(x => x.EndsWith("_x64.appx", StringComparison.Ordinal));
                    if (x64File == null)
                    {
                        _logger.LogCritical("Missing x64 file in WSL Ubuntu package extraction!");
                        _hostApplicationLifetime.StopApplication();
                        throw new OperationCanceledException(cancellationToken);
                    }
                    File.Move(x64File, Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion, "ubuntu-wsl.zip"));
                }
                await _assetManager.ExtractAsset("ubuntu-wsl.zip", Path.Combine(_pathProvider.RKMRoot, "ubuntu-wsl"), cancellationToken);

                _logger.LogInformation("Unregistering WSL distribution with the same name (if it exists)...");
                await RunWslInvocation(
                    new[] {
                        "--unregister",
                        _distroName,
                    },
                    string.Empty,
                    Encoding.Unicode,
                    cancellationToken);
                // We ignore the exit code of --unregister, since it will error if it doesn't exist.

                _logger.LogInformation("Importing WSL distribution...");
                var exitCode = await RunWslInvocation(
                    new[] {
                        "--import",
                        _distroName,
                        Path.Combine(_pathProvider.RKMRoot, "ubuntu-wsl"),
                        Path.Combine(_pathProvider.RKMRoot, "ubuntu-wsl", "install.tar.gz")
                    },
                    string.Empty,
                    Encoding.Unicode,
                    cancellationToken);
                if (exitCode != 0)
                {
                    _logger.LogCritical("Failed to configure WSL; see above for errors.");
                    _hostApplicationLifetime.StopApplication();
                    throw new OperationCanceledException(cancellationToken);
                }

                _installed = true;
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string> GetWslDistroName(CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException();
            }

            await EnsureInstalled(cancellationToken);
            return _distroName;
        }

        [SupportedOSPlatform("windows")]
        public async Task<string?> GetWslDistroMACAddress(CancellationToken cancellationToken)
        {
            var distroName = await GetWslDistroName(cancellationToken);

            var ipOutput = await CaptureWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/usr/sbin/ip", "address", "show", "eth0" }, Encoding.UTF8, cancellationToken);
            var macMatch = Regex.Match(ipOutput, "link/ether ([0-9a-f:]+)");
            if (!macMatch.Success)
            {
                return null;
            }
            var macAddressString = macMatch.Groups[1].Value;
            return macAddressString;
        }

        [SupportedOSPlatform("windows")]
        public async Task<IPAddress?> GetWslDistroIPAddress(CancellationToken cancellationToken)
        {
            var distroName = await GetWslDistroName(cancellationToken);

            var ipOutput = await CaptureWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/usr/sbin/ip", "address", "show", "eth0" }, Encoding.UTF8, cancellationToken);
            var ipMatch = Regex.Match(ipOutput, "inet ([0-9\\.]+)");
            if (!ipMatch.Success)
            {
                return null;
            }
            var ipAddressString = ipMatch.Groups[1].Value;
            if (IPAddress.TryParse(ipAddressString, out var ip))
            {
                return ip;
            }
            return null;
        }

        public void Dispose()
        {
            ((IDisposable)_semaphore).Dispose();
        }
    }
}
