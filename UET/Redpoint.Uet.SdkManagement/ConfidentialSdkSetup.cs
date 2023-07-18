namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Redpoint.ProcessExecution;
    using Redpoint.Registry;
    using System;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class ConfidentialSdkSetup : ISdkSetup
    {
        private readonly ConfidentialPlatformConfig _config;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<ConfidentialSdkSetup> _logger;

        public ConfidentialSdkSetup(
            string platformName,
            ConfidentialPlatformConfig config,
            IProcessExecutor processExecutor,
            ILogger<ConfidentialSdkSetup> logger)
        {
            PlatformName = platformName;
            _config = config;
            _processExecutor = processExecutor;
            _logger = logger;
        }

        public string PlatformName { get; }

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(_config.Version!);
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            if (_config.BeforeInstallSetRegistryValue != null)
            {
                ProcessRegistryKeys(_config.BeforeInstallSetRegistryValue);
            }

            try
            {
                var logPath = Path.Combine(sdkPackagePath, "InstallLogs");
                Directory.CreateDirectory(logPath);

                var interestedLogDirectories = new List<string>
                {
                    logPath,
                };
                if (_config.InstallerAdditionalLogFileDirectories != null)
                {
                    foreach (var directory in _config.InstallerAdditionalLogFileDirectories)
                    {
                        interestedLogDirectories.Add(directory);
                        foreach (var existingTxt in Directory.GetFiles(directory, "*.txt"))
                        {
                            File.Delete(existingTxt);
                        }
                        foreach (var existingLog in Directory.GetFiles(directory, "*.log"))
                        {
                            File.Delete(existingLog);
                        }
                    }
                }

                var monitoringCts = new CancellationTokenSource();
                var monitoringProcess = Task.Run(async () =>
                {
                    var logFiles = new Dictionary<string, long>();
                    while (!monitoringCts.IsCancellationRequested)
                    {
                        await Task.Delay(100);

                        foreach (var file in interestedLogDirectories.SelectMany(x => Directory.GetFiles(x, "*.txt").Concat(Directory.GetFiles(x, "*.log"))))
                        {
                            if (!logFiles.ContainsKey(file))
                            {
                                logFiles[file] = 0;
                            }

                            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            {
                                stream.Seek(logFiles[file], SeekOrigin.Begin);
                                var content = new byte[stream.Length - logFiles[file]];
                                stream.Read(content);
                                var contentString = Encoding.UTF8.GetString(content);
                                var lastNewline = contentString.LastIndexOf('\n');
                                if (lastNewline > 0)
                                {
                                    var targetContentString = lastNewline == contentString.Length - 1 ? contentString : contentString.Substring(0, lastNewline + 1);
                                    var byteCount = Encoding.UTF8.GetBytes(targetContentString).Length;
                                    logFiles[file] += byteCount;
                                    foreach (var line in targetContentString.Split('\n'))
                                    {
                                        var trimmedLine = line.TrimEnd();
                                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                                        {
                                            Console.WriteLine(trimmedLine);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = _config.InstallerPath!,
                        Arguments = _config.InstallerArguments!
                            .Select(x => x.Replace("%LOG_PATH%", logPath))
                            .ToArray(),
                        WorkingDirectory = Path.GetDirectoryName(_config.InstallerPath!)
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);

                monitoringCts.Cancel();
                try
                {
                    await monitoringProcess;
                }
                catch (OperationCanceledException) when (monitoringCts.IsCancellationRequested)
                {
                }

                if (_config.MustExistAfterInstall != null)
                {
                    foreach (var e in _config.MustExistAfterInstall)
                    {
                        if (!Path.Exists(e))
                        {
                            throw new SdkSetupPackageGenerationFailedException($"Expected the path '{e}' to exist after installation, but it did not.");
                        }
                    }
                }

                if (exitCode != 0 && !_config.PermitNonZeroExitCode)
                {
                    throw new SdkSetupPackageGenerationFailedException($"Confidential platform process exited with non-zero exit code: {exitCode}");
                }
            }
            finally
            {
                if (_config.AfterInstallSetRegistryValue != null)
                {
                    ProcessRegistryKeys(_config.AfterInstallSetRegistryValue);
                }
            }
        }

        private void ProcessRegistryKeys(
            Dictionary<string, Dictionary<string, JsonElement>> registryKeys)
        {
            foreach (var kv in registryKeys)
            {
                using (var stack = RegistryStack.OpenPath(kv.Key, true, true))
                {
                    foreach (var kvv in kv.Value)
                    {
                        switch (kvv.Value.ValueKind)
                        {
                            case JsonValueKind.String:
                                stack.Key.SetValue(kvv.Key, kvv.Value.GetString()!);
                                break;
                            case JsonValueKind.Number:
                                stack.Key.SetValue(kvv.Key, kvv.Value.GetInt32()!);
                                break;
                            default:
                                throw new NotSupportedException($"Can't set value of type {kvv.Value.ValueKind} into registry!");
                        }
                    }
                }
            }
        }

        public Task<EnvironmentForSdkUsage> EnsureSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = _config.EnvironmentVariables ?? new Dictionary<string, string>(),
            });
        }
    }
}
