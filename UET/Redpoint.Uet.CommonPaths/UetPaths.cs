namespace Redpoint.Uet.CommonPaths
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    public static class UetPaths
    {
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("linux")]
        [DllImport("libc")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern uint geteuid();

        private enum ApplicationName
        {
            UEFS,
            UET,
        };

        private static string GetApplicationDirectoryName(ApplicationName applicationName)
        {
            return applicationName switch
            {
                ApplicationName.UEFS => "UEFS",
                ApplicationName.UET => "UET",
                _ => throw new NotSupportedException(),
            };
        }

        private static readonly Lazy<string> _systemWideRootPath = new Lazy<string>(() =>
        {
            string basePath;
            if (OperatingSystem.IsWindows())
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            }
            else if (OperatingSystem.IsMacOS())
            {
                basePath = Path.Combine("/Users", "Shared");
            }
            else if (OperatingSystem.IsLinux())
            {
                basePath = Path.Combine("/opt");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
            return basePath;
        });

        private static string SystemWideRootPath => _systemWideRootPath.Value;

        private static string GetApplicationSystemWideRootPath(ApplicationName applicationName)
        {
            return Path.Combine(SystemWideRootPath, GetApplicationDirectoryName(applicationName));
        }

        private static string GetApplicationCurrentUserRootPath(ApplicationName applicationName)
        {
            bool isSystem;
            if (OperatingSystem.IsWindows())
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    isSystem = identity.IsSystem;
                }
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                isSystem = geteuid() == 0;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (isSystem)
            {
                return GetApplicationSystemWideRootPath(applicationName);
            }
            else
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    GetApplicationDirectoryName(applicationName));
            }
        }

        private static readonly Lazy<string> _uetRootPath = new Lazy<string>(() => GetApplicationSystemWideRootPath(ApplicationName.UET));

        private static readonly Lazy<string> _uetRunbackDirectoryPath = new Lazy<string>(() => Path.Combine(UetRootPath, "Runbacks"));

        private static readonly Lazy<string> _uetBuildReservationPath = new Lazy<string>(() =>
        {
            string reservationPath;
            if (OperatingSystem.IsWindows())
            {
                reservationPath = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                reservationPath = Path.Combine(SystemWideRootPath, ".ues");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
            return reservationPath;
        });

        private static readonly Lazy<string> _uetDefaultWindowsSdkStoragePath = new Lazy<string>(() =>
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "UET",
                "SDKs");
        });

        private static readonly Lazy<string> _uetDefaultMacSdkStoragePath = new Lazy<string>(() =>
        {
            return "/Users/Shared/UET/SDKs";
        });

        public static string UetRootPath => _uetRootPath.Value;

        public static string UetRunbackDirectoryPath => _uetRunbackDirectoryPath.Value;

        public static string UetBuildReservationPath => _uetBuildReservationPath.Value;

        public static string UetDefaultWindowsSdkStoragePath => _uetDefaultWindowsSdkStoragePath.Value;

        public static string UetDefaultMacSdkStoragePath => _uetDefaultMacSdkStoragePath.Value;

        private static readonly Lazy<string> _uefsLogsPath = new Lazy<string>(() => Path.Combine(GetApplicationSystemWideRootPath(ApplicationName.UEFS), "logs"));

        public static string UefsLogsPath => _uefsLogsPath.Value;

        private static readonly Lazy<string> _uefsRootPath = new Lazy<string>(() =>
        {
            if (!_isUefsRootPathInitialized)
            {
                throw new InvalidOperationException("UEFS root paths must be initialized with a call to InitUefsRootPath!");
            }

            return _uefsRootPathValue!;
        });

        public static string UefsRootPath => _uefsRootPath.Value;

        private static string _uefsRootPathValue = string.Empty;
        private static bool _isUefsRootPathInitialized = false;
        private static object _uefsRootPathInitializationLock = new object();

        private static (bool exists, bool mounted, string mountPoint) GetMacBuildVolumeMount()
        {
            var infoMounted = false;
            var infoMountPoint = string.Empty;
            var infoProc = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/sbin/diskutil",
                ArgumentList = { "info", "Build" },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });
            using (var reader = infoProc!.StandardOutput)
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine()?.Trim() ?? string.Empty;
                    if (line.StartsWith("Mounted:", StringComparison.OrdinalIgnoreCase))
                    {
                        infoMounted = line.Contains("Yes", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("Mount Point:", StringComparison.OrdinalIgnoreCase))
                    {
                        infoMountPoint = line.Substring(
                            line.IndexOf("Mount Point:", StringComparison.OrdinalIgnoreCase) +
                            "Mount Point:".Length).Trim();
                    }
                }
            }
            infoProc!.WaitForExit();
            return (infoProc.ExitCode == 0, infoMounted, infoMountPoint);
        }

        public static void InitUefsRootPath(Action<string> logInformation)
        {
            if (_isUefsRootPathInitialized)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(logInformation);

            if (!OperatingSystem.IsMacOS())
            {
                logInformation("UEFS root paths initialized on non-macOS platform.");
                _uefsRootPathValue = GetApplicationSystemWideRootPath(ApplicationName.UEFS);
                _isUefsRootPathInitialized = true;
                return;
            }

            // On macOS, we store UEFS data underneath a mounted "/Volumes/Build/UEFS" folder, if that
            // volume exists. We expect the volume to be an SSD with a larger storage space than the the
            // built-in SSD.
            lock (_uefsRootPathInitializationLock)
            {
                logInformation("Checking to see if 'Build' volume exists...");
                var currentInfo = GetMacBuildVolumeMount();
                if (!currentInfo.exists)
                {
                    logInformation("No 'Build' volume exists, using system drive.");
                    _uefsRootPathValue = GetApplicationSystemWideRootPath(ApplicationName.UEFS);
                    _isUefsRootPathInitialized = true;
                    return;
                }

            attemptMount:
                logInformation("Attempting to mount 'Build' volume...");
                {
                    var mountProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "/usr/sbin/diskutil",
                        ArgumentList = { "mount", "Build" },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    mountProc!.WaitForExit();
                    if (mountProc.ExitCode != 0)
                    {
                        throw new NotSupportedException("Unable to mount 'Build' disk when UEFS root path needs to be resolved.");
                    }
                }

                currentInfo = GetMacBuildVolumeMount();
                if (!currentInfo.exists)
                {
                    logInformation("'Build' volume went away!");
                    _uefsRootPathValue = GetApplicationSystemWideRootPath(ApplicationName.UEFS);
                    _isUefsRootPathInitialized = true;
                    return;
                }
                else if (currentInfo.mounted && !string.IsNullOrWhiteSpace(currentInfo.mountPoint))
                {
                    logInformation($"'Build' volume mounted at: '{currentInfo.mountPoint}'");
                    _uefsRootPathValue = Path.Combine(currentInfo.mountPoint, "UEFS");
                    _isUefsRootPathInitialized = true;
                    return;
                }
                else
                {
                    logInformation("Waiting for 'Build' to be mounted...");
                    Thread.Sleep(1000);
                    goto attemptMount;
                }
            }
        }
    }
}
