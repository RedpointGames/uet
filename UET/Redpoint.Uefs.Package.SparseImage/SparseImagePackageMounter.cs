namespace Redpoint.Uefs.Package.SparseImage
{
    using System.Diagnostics;
    using System.IO.Hashing;
    using System.Runtime.Versioning;
    using System.Text;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uefs.Protocol;

    [SupportedOSPlatform("macos")]
    internal sealed class SparseImagePackageMounter : IPackageMounter
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly ILogger<SparseImagePackageMounter> _logger;
        private bool _isMounted = false;
        private string? _mountPath;
        private string? _writeStoragePath;
        private WriteScratchPersistence _persistenceMode;
        internal static readonly byte[] _magicHeader = new byte[]
        {
            0x73, 0x70, 0x72, 0x73
        };

        public static Memory<byte> MagicHeader => _magicHeader;

        public bool RequiresAdminPermissions => false;

        // Not really compatible, but there's no native Docker for macOS anyway.
        public bool CompatibleWithDocker => true;

        public string WriteStoragePath => _writeStoragePath!;

        public SparseImagePackageMounter(
            IProcessExecutor processExecutor,
            IPathResolver pathResolver,
            ILogger<SparseImagePackageMounter> logger)
        {
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
            _logger = logger;
        }

        private SparseImagePackageMounter(
            IProcessExecutor processExecutor,
            IPathResolver pathResolver,
            ILogger<SparseImagePackageMounter> logger,
            ImportedMount mount) : this(
                processExecutor,
                pathResolver,
                logger)
        {
            _isMounted = true;
            _mountPath = mount.Devices.First(x => x.Value.mountPoint != null).Value.mountPoint!;
            _writeStoragePath = mount.Attributes["shadow-path"];
            if (mount.Attributes["shadow-path"].Contains("uefs-discard", StringComparison.Ordinal))
            {
                _persistenceMode = WriteScratchPersistence.DiscardOnUnmount;
            }
            else if (mount.Attributes["shadow-path"].Contains("uefs-keep", StringComparison.Ordinal))
            {
                _persistenceMode = WriteScratchPersistence.Keep;
            }
        }

        public async ValueTask MountAsync(
            string packagePath,
            string mountPath,
            string writeStoragePath,
            WriteScratchPersistence persistenceMode)
        {
            if (_isMounted)
            {
                throw new InvalidOperationException("Mount has already been called!");
            }

            _persistenceMode = persistenceMode;

            if (persistenceMode != WriteScratchPersistence.Keep)
            {
                // Delete the write storage path if it exists.
                if (Directory.Exists(writeStoragePath))
                {
                    Directory.Delete(writeStoragePath, true);
                }
                if (File.Exists(writeStoragePath))
                {
                    File.Delete(writeStoragePath);
                }
            }

            // Delete the mount path (non-recursively) if it exists.
            if (Directory.Exists(mountPath))
            {
                try
                {
                    Directory.Delete(mountPath);
                }
                catch (IOException ex) when (ex.Message.Contains("Resource busy", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PackageMounterException("unable to remove target path; ensure it is not already used as a mount point");
                }
            }
            Directory.CreateDirectory(mountPath);
            _mountPath = mountPath;

            // Create the write storage path as a directory.
            Directory.CreateDirectory(writeStoragePath);
            _writeStoragePath = writeStoragePath;

            // Get the package path, and the last modified time of the path, to make a unique hash for the shadow path. This prevents us from re-using a shadow path when either the base package changes or the file is written to.
            var hash = Hash.XxHash64(
                $"{File.GetLastWriteTimeUtc(packagePath).Ticks}-{packagePath}",
                Encoding.UTF8);

            // Mount our package at this path.
            var shadowPath = Path.Combine(writeStoragePath, $"uefs-{hash.Hash}-{(persistenceMode == WriteScratchPersistence.Keep ? "keep" : "discard")}.shadow");
            async Task<int> AttemptMount()
            {
                _logger.LogInformation($"Mounting package at path: {packagePath}");
                _logger.LogInformation($"Using shadow file at path: {shadowPath}");
                _logger.LogInformation($"Target mount path is: {_mountPath!}");
                return await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/bin/hdiutil",
                        Arguments =
                        [
                            "mount",
                            packagePath,
                            "-mountpoint",
                            _mountPath!,
                            "-shadow",
                            shadowPath,
                        ]
                    },
                    CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                    {
                        ReceiveStdout = line =>
                        {
                            _logger.LogInformation($"hdiutil stdout: {line}");
                            return false;
                        },
                        ReceiveStderr = line =>
                        {
                            _logger.LogInformation($"hdiutil stderr: {line}");
                            return false;
                        },
                    }),
                    CancellationToken.None).ConfigureAwait(false);
            }
            var hdiutilExitCode = await AttemptMount();
            if (hdiutilExitCode != 0 && File.Exists(shadowPath))
            {
                _logger.LogWarning($"Failed to mount, attempting to delete shadow path and retrying: {shadowPath}");
                File.Delete(shadowPath);
                hdiutilExitCode = await AttemptMount();
            }
            if (hdiutilExitCode != 0)
            {
                throw new PackageMounterException($"hdiutil for mounting exited with code {hdiutilExitCode}");
            }
            _isMounted = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isMounted)
            {
                var hdiutilExitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/bin/hdiutil",
                        Arguments =
                        [
                            // note: 'unmount' doesn't detach the 
                            // disk and leaves it locked, so we use 'detach'
                            "detach",
                            _mountPath!,
                            "-force",
                        ]
                    },
                    CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                    {
                        ReceiveStdout = line =>
                        {
                            _logger.LogInformation($"hdiutil detach stdout: {line}");
                            return false;
                        },
                        ReceiveStderr = line =>
                        {
                            _logger.LogInformation($"hdiutil detach stderr: {line}");
                            return false;
                        },
                    }),
                    CancellationToken.None).ConfigureAwait(false);
                if (hdiutilExitCode != 0)
                {
                    throw new PackageMounterException($"hdiutil for unmounting exited with code {hdiutilExitCode}");
                }
                _isMounted = false;

                // Clean up the write storage path as well.
                if (_persistenceMode != WriteScratchPersistence.Keep)
                {
                    if (Directory.Exists(_writeStoragePath!))
                    {
                        Directory.Delete(_writeStoragePath, true);
                    }
                }
            }
        }

        private struct ImportedMount
        {
            public ImportedMount()
            {
            }

            public Dictionary<string, string> Attributes = new Dictionary<string, string>();
            public Dictionary<string, (string type, string? mountPoint)> Devices = new Dictionary<string, (string type, string? mountPoint)>();
        }

        public async Task<(string packagePath, string mountPath, IPackageMounter mounter)[]> ImportExistingMountsAtStartupAsync()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/hdiutil",
                ArgumentList = {
                    "info"
                },
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
            };
            var process = new Process();
            process.StartInfo = processInfo;
            process.EnableRaisingEvents = true;
            if (!process.Start())
            {
                throw new PackageMounterException("unable to start hdiutil process for unmounting sparse APFS image");
            }
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new PackageMounterException($"hdiutil for unmounting exited with code {process.ExitCode}");
            }

            var allImportedMounts = new List<ImportedMount>();
            var currentImportedMount = new ImportedMount();
            var mode = "global";
            foreach (var line in output.Split("\n"))
            {
                if (mode == "global")
                {
                    if (line.StartsWith("=======", StringComparison.Ordinal))
                    {
                        mode = "local";
                        continue;
                    }
                }
                else if (mode == "local")
                {
                    if (line.StartsWith("/dev", StringComparison.Ordinal))
                    {
                        var components = line.Split("\t", 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var deviceName = components.Length > 0 ? components[0] : null;
                        var deviceType = components.Length > 1 ? components[1] : null;
                        var mountPath = components.Length > 2 ? components[2] : null;
                        if (string.IsNullOrWhiteSpace(deviceType)) { deviceType = null; }
                        if (string.IsNullOrWhiteSpace(mountPath)) { mountPath = null; }
                        if (deviceName != null && deviceType != null)
                        {
                            currentImportedMount.Devices.Add(deviceName, (deviceType, mountPath));
                        }
                    }
                    else if (line.Contains(':', StringComparison.Ordinal))
                    {
                        var attributePair = line.Split(":", 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        currentImportedMount.Attributes.Add(attributePair[0], attributePair[1]);
                    }
                    else if (line.StartsWith("========", StringComparison.Ordinal))
                    {
                        if (currentImportedMount.Attributes.Count > 0)
                        {
                            allImportedMounts.Add(currentImportedMount);
                        }
                        currentImportedMount = new ImportedMount();
                    }
                }
            }
            if (currentImportedMount.Attributes.Count > 0)
            {
                allImportedMounts.Add(currentImportedMount);
            }

            var result = new List<(string packagePath, string mountPath, IPackageMounter mounter)>();
            foreach (var importedMount in allImportedMounts)
            {
                if (importedMount.Attributes.TryGetValue("image-type", out var imageType) &&
                    imageType == "sparse disk image (shadowed)" &&
                    importedMount.Attributes.TryGetValue("image-path", out var imagePath) &&
                    importedMount.Attributes.ContainsKey("shadow-path") &&
                    importedMount.Devices.Any(x => x.Value.mountPoint != null))
                {
                    result.Add((
                        imagePath,
                        importedMount.Devices.First(x => x.Value.mountPoint != null).Value.mountPoint!,
                        new SparseImagePackageMounter(_processExecutor, _pathResolver, _logger, importedMount)));
                }
            }
            return result.ToArray();
        }
    }
}
