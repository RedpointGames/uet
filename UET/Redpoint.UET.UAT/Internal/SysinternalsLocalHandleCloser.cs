namespace Redpoint.UET.UAT.Internal
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System.Diagnostics;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class SysinternalsLocalHandleCloser : ILocalHandleCloser
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<SysinternalsLocalHandleCloser> _logger;
        private static readonly Regex _handle64Regex = new Regex("pid: ([0-9]+)\\s+type: File\\s+([A-F0-9]+):\\s+(.+)");

        public SysinternalsLocalHandleCloser(
            IProcessExecutor processExecutor,
            ILogger<SysinternalsLocalHandleCloser> logger)
        {
            _processExecutor = processExecutor;
            _logger = logger;
        }

        private class HandleTerminatingCaptureSpecification : ICaptureSpecification
        {
            private readonly ILogger<SysinternalsLocalHandleCloser> _logger;

            public HandleTerminatingCaptureSpecification(
                ILogger<SysinternalsLocalHandleCloser> logger)
            {
                _logger = logger;
            }

            public List<(string handle, string pid, string handlePath)> HandlesToForciblyClose = new List<(string handle, string pid, string handlePath)>();

            public bool InterceptStandardInput => true;

            public bool InterceptStandardOutput => true;

            public bool InterceptStandardError => false;

            public void OnReceiveStandardError(string data)
            {
                throw new NotSupportedException();
            }

            public void OnReceiveStandardOutput(string data)
            {
                Console.WriteLine(data);
                var match = _handle64Regex.Match(data.Trim());
                if (match.Success)
                {
                    var pid = match.Groups[1].Value.Trim();
                    var handle = match.Groups[2].Value.Trim();
                    var handlePath = match.Groups[3].Value.Trim();

                    var terminatedHandle = false;
                    Process? process = null;
                    try
                    {
                        process = Process.GetProcessById(int.Parse(pid));
                    }
                    catch
                    {
                    }
                    if (process != null)
                    {
                        try
                        {
                            process.Kill();
                            _logger.LogInformation($"Killed process {process.Id} '{process.ProcessName}' because it has got an open handle to: {handlePath}");
                            terminatedHandle = true;
                        }
                        catch
                        {
                        }
                    }

                    if (!terminatedHandle)
                    {
                        HandlesToForciblyClose.Add((handle, pid, handlePath));
                    }
                }
            }

            public string? OnRequestStandardInputAtStartup()
            {
                return null;
            }
        }

        public async Task CloseLocalHandles(string localPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            // Make sure we have handle64.exe available first.
            var expectedPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "handle64.exe");
            var handle64StreamName = $"Redpoint.UET.UAT.Internal.handle64.exe";
            if (!File.Exists(expectedPath))
            {
                _logger.LogInformation("Extracting handle64.exe...");
                using (var outputStream = new FileStream($"{expectedPath}-{Process.GetCurrentProcess().Id}", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var inputStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(handle64StreamName))
                    {
                        await inputStream!.CopyToAsync(outputStream);
                    }
                }
                File.Move($"{expectedPath}-{Process.GetCurrentProcess().Id}", expectedPath, true);
            }

            // Run handle64.exe, terminating processes and getting a list of handles to forcibly close otherwise.
            _logger.LogInformation($"Scanning for open handles underneath: {localPath}");
            var captureSpecification = new HandleTerminatingCaptureSpecification(_logger);
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = expectedPath,
                    Arguments = new[]
                    {
                        "-accepteula",
                        "-nobanner",
                        localPath
                    }
                },
                captureSpecification,
                CancellationToken.None);
            foreach (var (handle, pid, handlePath) in captureSpecification.HandlesToForciblyClose)
            {
                _logger.LogInformation($"Forcibly closing handle {handle} in process {pid} because it is an open handle to: {handlePath}");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = expectedPath,
                        Arguments = new[]
                        {
                            "-accepteula",
                            "-nobanner",
                            "-c",
                            handle,
                            "-p",
                            pid,
                            "-y"
                        }
                    },
                    CaptureSpecification.Passthrough,
                    CancellationToken.None);
                if (exitCode != 0)
                {
                    _logger.LogWarning($"Unable to terminate process or close handle to release: {handlePath}");
                }
            }
        }
    }
}
