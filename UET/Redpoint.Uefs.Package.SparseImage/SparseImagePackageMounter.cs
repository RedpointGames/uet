using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Redpoint.Uefs.Protocol;

namespace Redpoint.Uefs.Package.SparseImage
{
    [SupportedOSPlatform("macos")]
    internal class SparseImagePackageMounter : IPackageMounter
    {
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

        public SparseImagePackageMounter(ILogger? logger)
        {
        }

        private SparseImagePackageMounter(ImportedMount mount)
        {
            _isMounted = true;
            _mountPath = mount.Devices.First(x => x.Value.mountPoint != null).Value.mountPoint!;
            _writeStoragePath = mount.Attributes["shadow-path"];
            if (mount.Attributes["shadow-path"].Contains("uefs-discard"))
            {
                _persistenceMode = WriteScratchPersistence.DiscardOnUnmount;
            }
            else if (mount.Attributes["shadow-path"].Contains("uefs-keep"))
            {
                _persistenceMode = WriteScratchPersistence.Keep;
            }
        }

        public ValueTask MountAsync(
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
                catch (IOException ex) when (ex.Message.Contains("Resource busy"))
                {
                    throw new PackageMounterException("unable to remove target path; ensure it is not already used as a mount point");
                }
            }
            Directory.CreateDirectory(mountPath);
            _mountPath = mountPath;

            // Create the write storage path as a directory.
            Directory.CreateDirectory(writeStoragePath);
            _writeStoragePath = writeStoragePath;

            // Mount our package at this path.
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/hdiutil",
                    ArgumentList = {
                        "mount",
                        packagePath,
                        "-mountpoint",
                        _mountPath,
                        "-shadow",
                        Path.Combine(writeStoragePath, $"uefs-{(persistenceMode == WriteScratchPersistence.Keep ? "keep" : "discard")}.shadow"),
                    },
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                var process = new Process();
                process.StartInfo = processInfo;
                process.EnableRaisingEvents = true;
                if (!process.Start())
                {
                    throw new PackageMounterException("unable to start hdiutil process for mounting package");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new PackageMounterException($"hdiutil for mounting exited with code {process.ExitCode}");
                }
                _isMounted = true;
            }

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_isMounted)
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/hdiutil",
                    ArgumentList = {
                        // note: 'unmount' doesn't detach the 
                        // disk and leaves it locked, so we use 'detach'
                        "detach",
                        _mountPath!,
                        "-force"
                    },
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                var process = new Process();
                process.StartInfo = processInfo;
                process.EnableRaisingEvents = true;
                if (!process.Start())
                {
                    throw new PackageMounterException("unable to start hdiutil process for unmounting sparse APFS image");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new PackageMounterException($"hdiutil for unmounting exited with code {process.ExitCode}");
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
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
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
                    if (line.StartsWith("======="))
                    {
                        mode = "local";
                        continue;
                    }
                }
                else if (mode == "local")
                {
                    if (line.StartsWith("/dev"))
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
                    else if (line.Contains(":"))
                    {
                        var attributePair = line.Split(":", 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        currentImportedMount.Attributes.Add(attributePair[0], attributePair[1]);
                    }
                    else if (line.StartsWith("========"))
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
                if (importedMount.Attributes.ContainsKey("image-type") &&
                    importedMount.Attributes["image-type"] == "sparse disk image (shadowed)" &&
                    importedMount.Attributes.ContainsKey("image-path") &&
                    importedMount.Attributes.ContainsKey("shadow-path") &&
                    importedMount.Devices.Any(x => x.Value.mountPoint != null))
                {
                    result.Add((
                        importedMount.Attributes["image-path"],
                        importedMount.Devices.First(x => x.Value.mountPoint != null).Value.mountPoint!,
                        new SparseImagePackageMounter(importedMount)));
                }
            }
            return result.ToArray();
        }
    }
}
