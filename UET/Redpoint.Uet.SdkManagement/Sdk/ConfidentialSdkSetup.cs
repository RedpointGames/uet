namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.Registry;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using Redpoint.Uet.SdkManagement.Sdk.MsiExtract;
    using System;
    using System.Globalization;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class ConfidentialSdkSetup : ISdkSetup
    {
        protected readonly ConfidentialPlatformConfig _config;
        private readonly string _substituteVersion;
        private readonly GenericPlatformVersion _substituteVersionParsed;
        private readonly IProcessExecutor _processExecutor;
        private readonly IStringUtilities _stringUtilities;
        private readonly WindowsSdkInstaller _windowsSdkInstaller;
        private readonly ILogger<ConfidentialSdkSetup> _logger;
        private readonly IMsiExtraction _msiExtraction;

        public ConfidentialSdkSetup(
            string[] platformNames,
            ConfidentialPlatformConfig config,
            string substituteVersion,
            IProcessExecutor processExecutor,
            IStringUtilities stringUtilities,
            WindowsSdkInstaller windowsSdkInstaller,
            ILogger<ConfidentialSdkSetup> logger,
            IMsiExtraction msiExtraction)
        {
            PlatformNames = platformNames;
            _config = config;
            _substituteVersion = substituteVersion;
            _substituteVersionParsed = GenericPlatformVersion.Parse(substituteVersion)!;
            _processExecutor = processExecutor;
            _stringUtilities = stringUtilities;
            _windowsSdkInstaller = windowsSdkInstaller;
            _logger = logger;
            _msiExtraction = msiExtraction;
        }

        public IReadOnlyList<string> PlatformNames { get; }

        public string CommonPlatformNameForPackageId => _config.CommonPlatformName ?? PlatformNames[0];

        public bool SupportsTemporaryFolderSwapOnInstall => _config.SupportsTemporaryFolderSwapOnInstall ?? true;

        private string Substitute(string value, string? sdkPackagePath)
        {
            var minorNearest500 = (long)(Math.Round((double)(_substituteVersionParsed.Minor) / 500.0) * 500);

            value = value
                .Replace("%VERSION%", _substituteVersion, StringComparison.Ordinal)
                .Replace("%VERSION_MAJOR%", _substituteVersionParsed.Major.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("%VERSION_MINOR_NEAREST_500%", minorNearest500.ToString("D3", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("%INSTALLTIME_APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.Ordinal)
                .Replace("%INSTALLTIME_LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(sdkPackagePath))
            {
                value = value.Replace("%SDK_PACKAGE_PATH%", sdkPackagePath, StringComparison.Ordinal);
            }

            return value;
        }

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Substitute(_config.Version ?? _substituteVersion, null));
        }

        public virtual async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            foreach (var message in _config.Messages ?? [])
            {
                _logger.LogInformation(Substitute(message, sdkPackagePath));
            }

            foreach (var installer in _config.Installers ?? Array.Empty<ConfidentialPlatformConfigInstaller>())
            {
                if (installer.BeforeInstallSetRegistryValue != null)
                {
                    ProcessRegistryKeys(installer.BeforeInstallSetRegistryValue, sdkPackagePath);
                }

                var installerPath = Substitute(installer.InstallerPath!, sdkPackagePath);

                try
                {
                    if (installer.BeforeInstallDeleteDirectory != null)
                    {
                        foreach (var e in installer.BeforeInstallDeleteDirectory)
                        {
                            var eSubstituted = Substitute(e, sdkPackagePath);
                            if (Directory.Exists(eSubstituted))
                            {
                                _logger.LogInformation($"Deleting existing installation directory: {eSubstituted} (this might take a while)");
                                await DirectoryAsync.DeleteAsync(eSubstituted, true);
                            }
                        }
                    }

                    var logPath = Path.Combine(sdkPackagePath, _stringUtilities.GetStabilityHash(installerPath, null), "InstallLogs");
                    Directory.CreateDirectory(logPath);

                    var interestedLogDirectories = new List<string>
                    {
                        logPath,
                    };
                    if (installer.InstallerAdditionalLogFileDirectories != null)
                    {
                        foreach (var directoryUnsub in installer.InstallerAdditionalLogFileDirectories)
                        {
                            var directory = Substitute(directoryUnsub, sdkPackagePath);
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
                                if (!logFiles.TryGetValue(file, out long logFilePosition))
                                {
                                    logFilePosition = 0;
                                    logFiles[file] = logFilePosition;
                                }

                                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    stream.Seek(logFilePosition, SeekOrigin.Begin);
                                    var content = new byte[stream.Length - logFilePosition];
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

                    _logger.LogInformation($"Executing installer at '{installerPath}'...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = installerPath,
                            Arguments = installer.InstallerArguments!
                                .Select(x => Substitute(x.Replace("%LOG_PATH%", logPath, StringComparison.Ordinal), sdkPackagePath))
                                .Select(x => new LogicalProcessArgument(x))
                                .ToArray(),
                            WorkingDirectory = Path.GetDirectoryName(installerPath)
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
                            var eSubstituted = Substitute(e, sdkPackagePath);
                            if (!Path.Exists(eSubstituted))
                            {
                                throw new SdkSetupPackageGenerationFailedException($"Expected the path '{eSubstituted}' to exist after installation, but it did not.");
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
                        ProcessRegistryKeys(installer.AfterInstallSetRegistryValue, sdkPackagePath);
                    }
                }
            }

            foreach (var extractor in _config.Extractors ?? Array.Empty<ConfidentialPlatformConfigExtractor>())
            {
                foreach (var file in Directory.GetFiles(Substitute(extractor.MsiSourceDirectory!, sdkPackagePath), Substitute(extractor.MsiFilenameFilter ?? "*.msi", sdkPackagePath)))
                {
                    var targetDirectory = Path.Combine(sdkPackagePath, Substitute(extractor.ExtractionSubdirectoryPath!.Replace('/', '\\'), sdkPackagePath));
                    await _msiExtraction.ExtractMsiAsync(
                        Substitute(extractor.MsiSourceDirectory!, sdkPackagePath),
                        file,
                        targetDirectory,
                        cancellationToken);
                }
            }

            if (_config.AutoSdkSetupScripts != null)
            {
                foreach (var setupScript in _config.AutoSdkSetupScripts)
                {
                    var targetPath = Path.Combine(sdkPackagePath, Substitute(setupScript.TargetPath!.Replace('/', '\\'), sdkPackagePath));
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    var content = Substitute(string.Join(Environment.NewLine, setupScript.Lines!), sdkPackagePath);
                    var existingContent = File.Exists(targetPath) ? File.ReadAllText(targetPath) : string.Empty;
                    if (existingContent != content)
                    {
                        File.WriteAllText(targetPath, content);
                    }
                }
            }

            if (_config.RequiredWindowsSdk != null)
            {
                var windowsSdkPreferredVersion = VersionNumber.Parse(Substitute(_config.RequiredWindowsSdk.WindowsSdkPreferredVersion!, sdkPackagePath));
                var visualCppMinimumVersion = VersionNumber.Parse(Substitute(_config.RequiredWindowsSdk.VisualCppMinimumVersion!, sdkPackagePath));
                var suggestedComponents = _config.RequiredWindowsSdk.SuggestedComponents ?? Array.Empty<string>();

                await _windowsSdkInstaller.InstallSdkToPath(
                    new WindowsSdkInstallerTarget
                    {
                        WindowsSdkPreferredVersion = windowsSdkPreferredVersion,
                        MinimumVisualCppVersion = visualCppMinimumVersion,
                        PreferredVisualCppVersions = new(),
                        BannedVisualCppVersions = new(),
                        SuggestedComponents = suggestedComponents.Select(x => Substitute(x, sdkPackagePath)).ToArray(),
                        MinimumRequiredClangVersions = [],
                    },
                    string.IsNullOrWhiteSpace(_config.RequiredWindowsSdk.SubdirectoryName) ? sdkPackagePath : Path.Combine(sdkPackagePath, Substitute(_config.RequiredWindowsSdk.SubdirectoryName, sdkPackagePath)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private void ProcessRegistryKeys(
            Dictionary<string, Dictionary<string, JsonElement>> registryKeys,
            string sdkPackagePath)
        {
            foreach (var kv in registryKeys)
            {
                using (var stack = RegistryStack.OpenPath(Substitute(kv.Key, sdkPackagePath), true, true))
                {
                    foreach (var kvv in kv.Value)
                    {
                        switch (kvv.Value.ValueKind)
                        {
                            case JsonValueKind.String:
                                stack.Key.SetValue(kvv.Key, Substitute(kvv.Value.GetString()!, sdkPackagePath));
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
                RelativePathInsideAutoSdkPath = Substitute(x.Key, sdkPackagePath),
                RelativePathInsideSdkPackagePath = Substitute(x.Value, sdkPackagePath)
            }).ToArray());
        }

        public Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = _config.EnvironmentVariables == null
                    ? new()
                    : _config.EnvironmentVariables.ToDictionary(k => Substitute(k.Key, sdkPackagePath), v => Substitute(v.Value, sdkPackagePath)),
            });
        }
    }
}
