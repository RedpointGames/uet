namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Registry;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using System;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class ConfidentialSdkSetup : ISdkSetup
    {
        protected readonly ConfidentialPlatformConfig _config;
        private readonly IProcessExecutor _processExecutor;
        private readonly IStringUtilities _stringUtilities;
        private readonly WindowsSdkInstaller _windowsSdkInstaller;
        private readonly ILogger<ConfidentialSdkSetup> _logger;

        public ConfidentialSdkSetup(
            string platformName,
            ConfidentialPlatformConfig config,
            IProcessExecutor processExecutor,
            IStringUtilities stringUtilities,
            WindowsSdkInstaller windowsSdkInstaller,
            ILogger<ConfidentialSdkSetup> logger)
        {
            PlatformNames = new[] { platformName };
            _config = config;
            _processExecutor = processExecutor;
            _stringUtilities = stringUtilities;
            _windowsSdkInstaller = windowsSdkInstaller;
            _logger = logger;
        }

        public IReadOnlyList<string> PlatformNames { get; }

        public string CommonPlatformNameForPackageId => _config.CommonPlatformName ?? PlatformNames[0];

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(_config.Version!);
        }

        public virtual async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            foreach (var installer in _config.Installers ?? Array.Empty<ConfidentialPlatformConfigInstaller>())
            {
                if (installer.BeforeInstallSetRegistryValue != null)
                {
                    ProcessRegistryKeys(installer.BeforeInstallSetRegistryValue);
                }

                try
                {
                    var logPath = Path.Combine(sdkPackagePath, _stringUtilities.GetStabilityHash(installer.InstallerPath!, null), "InstallLogs");
                    Directory.CreateDirectory(logPath);

                    var interestedLogDirectories = new List<string>
                    {
                        logPath,
                    };
                    if (installer.InstallerAdditionalLogFileDirectories != null)
                    {
                        foreach (var directory in installer.InstallerAdditionalLogFileDirectories)
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

                    using var monitoringCts = new CancellationTokenSource();
                    var monitoringProcess = Task.Run(async () =>
                    {
                        var logFiles = new Dictionary<string, long>();
                        while (!monitoringCts.IsCancellationRequested)
                        {
                            await Task.Delay(100).ConfigureAwait(false);

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
                                        var targetContentString = lastNewline == contentString.Length - 1 ? contentString : contentString[..(lastNewline + 1)];
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
                    }, monitoringCts.Token);

                    _logger.LogInformation($"Executing installer at '{installer.InstallerPath!}'...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = installer.InstallerPath!,
                            Arguments = installer.InstallerArguments!
                                .Select(x => x.Replace("%LOG_PATH%", logPath, StringComparison.Ordinal))
                                .ToArray(),
                            WorkingDirectory = Path.GetDirectoryName(installer.InstallerPath!)
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);

                    monitoringCts.Cancel();
                    try
                    {
                        await monitoringProcess.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (monitoringCts.IsCancellationRequested)
                    {
                    }

                    if (installer.MustExistAfterInstall != null)
                    {
                        foreach (var e in installer.MustExistAfterInstall)
                        {
                            if (!Path.Exists(e))
                            {
                                throw new SdkSetupPackageGenerationFailedException($"Expected the path '{e}' to exist after installation, but it did not.");
                            }
                        }
                    }

                    if (exitCode != 0 && !installer.PermitNonZeroExitCode)
                    {
                        throw new SdkSetupPackageGenerationFailedException($"Confidential platform process exited with non-zero exit code: {exitCode}");
                    }
                }
                finally
                {
                    if (installer.AfterInstallSetRegistryValue != null)
                    {
                        ProcessRegistryKeys(installer.AfterInstallSetRegistryValue);
                    }
                }
            }

            foreach (var extractor in _config.Extractors ?? Array.Empty<ConfidentialPlatformConfigExtractor>())
            {
                foreach (var file in Directory.GetFiles(extractor.MsiSourceDirectory!, extractor.MsiFilenameFilter ?? "*.msi"))
                {
                    var targetDirectory = Path.Combine(sdkPackagePath, extractor.ExtractionSubdirectoryPath!.Replace('/', '\\'));
                    _logger.LogInformation($"Extracting MSI from '{file}' to '{targetDirectory}'...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                            Arguments = new[]
                            {
                                "/a",
                                file,
                                "/quiet",
                                "/qn",
                                $"TARGETDIR={targetDirectory}",
                            },
                            WorkingDirectory = Path.GetDirectoryName(file)
                        }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);
                    if (!File.Exists(Path.Combine(targetDirectory, Path.GetFileName(file))))
                    {
                        throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {file}");
                    }
                    File.Delete(Path.Combine(targetDirectory, Path.GetFileName(file)));
                }
            }

            if (_config.AutoSdkSetupScripts != null)
            {
                foreach (var setupScript in _config.AutoSdkSetupScripts)
                {
                    var targetPath = Path.Combine(sdkPackagePath, setupScript.TargetPath!.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    var content = string.Join(Environment.NewLine, setupScript.Lines!);
                    var existingContent = File.Exists(targetPath) ? File.ReadAllText(targetPath) : string.Empty;
                    if (existingContent != content)
                    {
                        File.WriteAllText(targetPath, content);
                    }
                }
            }

            if (_config.RequiredWindowsSdk != null)
            {
                var windowsSdkPreferredVersion = VersionNumber.Parse(_config.RequiredWindowsSdk.WindowsSdkPreferredVersion!);
                var visualCppMinimumVersion = VersionNumber.Parse(_config.RequiredWindowsSdk.VisualCppMinimumVersion!);
                var suggestedComponents = _config.RequiredWindowsSdk.SuggestedComponents ?? Array.Empty<string>();

                await _windowsSdkInstaller.InstallSdkToPath(
                    new WindowsSdkInstallerTarget
                    {
                        WindowsSdkPreferredVersion = windowsSdkPreferredVersion,
                        VisualCppMinimumVersion = visualCppMinimumVersion,
                        SuggestedComponents = suggestedComponents,
                    },
                    string.IsNullOrWhiteSpace(_config.RequiredWindowsSdk.SubdirectoryName) ? sdkPackagePath : Path.Combine(sdkPackagePath, _config.RequiredWindowsSdk.SubdirectoryName),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static void ProcessRegistryKeys(
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

        public Task<AutoSdkMapping[]> GetAutoSdkMappingsForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            var mappings = _config.AutoSdkRelativePathMappings ?? new Dictionary<string, string>();
            return Task.FromResult(mappings.Select(x => new AutoSdkMapping
            {
                RelativePathInsideAutoSdkPath = x.Key,
                RelativePathInsideSdkPackagePath = x.Value
            }).ToArray());
        }

        public Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = _config.EnvironmentVariables ?? new Dictionary<string, string>(),
            });
        }
    }
}
