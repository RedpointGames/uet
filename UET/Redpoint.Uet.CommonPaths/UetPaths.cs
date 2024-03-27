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
            OpenGE,
            UEFS,
            UET,
        };

        private static string GetApplicationDirectoryName(ApplicationName applicationName)
        {
            return applicationName switch
            {
                ApplicationName.OpenGE => "OpenGE",
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
                basePath = Path.Combine("/tmp");
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

        private static readonly Lazy<string> _opengeRootPath = new Lazy<string>(() => GetApplicationSystemWideRootPath(ApplicationName.OpenGE));

        private static readonly Lazy<string> _opengeUserSpecificCachePath = new Lazy<string>(() => Path.Combine(
            GetApplicationCurrentUserRootPath(ApplicationName.OpenGE),
            "Cache"));

        private static readonly Lazy<string> _uefsRootPath = new Lazy<string>(() =>
        {
            // On macOS, we store UEFS data underneath a mounted "/Volumes/Build/UEFS" folder, if that
            // volume exists. We expect the volume to be an SSD with a larger storage space than the the
            // built-in SSD.
            if (OperatingSystem.IsMacOS())
            {
                var infoProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/sbin/diskutil",
                    ArgumentList = { "info", "Build" },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                infoProc!.WaitForExit();
                if (infoProc.ExitCode == 0)
                {
                    // We have a "Build" disk we can mount.
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

                    Directory.CreateDirectory("/Volumes/Build/UEFS");
                    return "/Volumes/Build/UEFS";
                }
            }

            return GetApplicationSystemWideRootPath(ApplicationName.UEFS);
        });

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

        public static string OpenGERootPath => _opengeRootPath.Value;

        public static string OpenGEUserSpecificCachePath => _opengeUserSpecificCachePath.Value;

        public static string UefsRootPath => _uefsRootPath.Value;

        public static string UetRootPath => _uetRootPath.Value;

        public static string UetRunbackDirectoryPath => _uetRunbackDirectoryPath.Value;

        public static string UetBuildReservationPath => _uetBuildReservationPath.Value;

        public static string UetDefaultWindowsSdkStoragePath => _uetDefaultWindowsSdkStoragePath.Value;

        public static string UetDefaultMacSdkStoragePath => _uetDefaultMacSdkStoragePath.Value;
    }
}
