namespace Redpoint.MSBuildResolution
{
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Registry;
    using System.Runtime.Versioning;

    internal sealed class DefaultMSBuildPathResolver : IMSBuildPathResolver
    {
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        public DefaultMSBuildPathResolver(
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        [SupportedOSPlatform("windows")]
        private static string? GetMSBuildFromRegistry(string path, string key, string suffix)
        {
            var pathsToTry = new[]
            {
                @$"HKCU:\SOFTWARE\{path}",
                @$"HKLM:\SOFTWARE\{path}",
                @$"HKCU:\SOFTWARE\Wow6432Node\{path}",
                @$"HKLM:\SOFTWARE\Wow6432Node\{path}"
            };
            foreach (var pathToTry in pathsToTry)
            {
                using (var stack = RegistryStack.OpenPath(pathToTry))
                {
                    if (stack.Exists)
                    {
                        var property = stack.Key.GetValue(key);
                        if (property is string)
                        {
                            return $"{property}{suffix}";
                        }
                    }
                }
            }
            return null;
        }

        public async Task<(string path, LogicalProcessArgument[] preargs)> ResolveMSBuildPath()
        {
            if (OperatingSystem.IsMacOS())
            {
                return (
                    await _pathResolver.ResolveBinaryPath("dotnet").ConfigureAwait(false),
                    ["msbuild"]);
            }
            else if (OperatingSystem.IsWindows())
            {
                var vswherePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft Visual Studio",
                    "Installer",
                    "vswhere.exe");
                if (File.Exists(vswherePath))
                {
                    var installationPath = string.Empty;
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = vswherePath,
                            Arguments =
                            [
                                "-prerelease",
                                "-latest",
                                "-products",
                                "*",
                                "-requires",
                                "Microsoft.Component.MSBuild",
                                "-property",
                                "installationPath"
                            ]
                        },
                        CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                        {
                            ReceiveStdout = (line) =>
                            {
                                installationPath += line + "\n";
                                return false;
                            }
                        }),
                        CancellationToken.None).ConfigureAwait(false);
                    installationPath = installationPath.Trim();
                    var currentPath = Path.Combine(installationPath, "MSBuild", "Current", "Bin", "MSBuild.exe");
                    var versionedPath = Path.Combine(installationPath, "MSBuild", "15.0", "Bin", "MSBuild.exe");
                    if (File.Exists(currentPath))
                    {
                        return (currentPath, Array.Empty<LogicalProcessArgument>());
                    }
                    if (File.Exists(versionedPath))
                    {
                        return (versionedPath, Array.Empty<LogicalProcessArgument>());
                    }
                }

                var proposedPath = GetMSBuildFromRegistry(@"Microsoft\VisualStudio\SxS\VS7", "15.0", @"MSBuild\15.0\bin\MSBuild.exe");
                if (proposedPath != null)
                {
                    return (proposedPath, Array.Empty<LogicalProcessArgument>());
                }
                proposedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSBuild", "14.0", "Bin", "MSBuild.exe");
                if (File.Exists(proposedPath))
                {
                    return (proposedPath, Array.Empty<LogicalProcessArgument>());
                }
                proposedPath = GetMSBuildFromRegistry(@"Microsoft\MSBuild\ToolsVersions\14.0", "MSBuildToolsPath", "MSBuild.exe");
                if (proposedPath != null)
                {
                    return (proposedPath, Array.Empty<LogicalProcessArgument>());
                }
                proposedPath = GetMSBuildFromRegistry(@"Microsoft\MSBuild\ToolsVersions\12.0", "MSBuildToolsPath", "MSBuild.exe");
                if (proposedPath != null)
                {
                    return (proposedPath, Array.Empty<LogicalProcessArgument>());
                }

                throw new FileNotFoundException($"MSBuild could not be found.");
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}