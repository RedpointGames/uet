namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using Redpoint.PathResolution;
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    internal class DefaultDotnetLocator : IDotnetLocator
    {
        private readonly IPathResolver _pathResolver;

        public DefaultDotnetLocator(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        public async Task<string?> TryLocateDotNetWithinEngine(string enginePath)
        {
            string? dotnetPath = null;
            var dotnetEnginePath = Path.Combine(enginePath, "Engine", "Binaries", "ThirdParty", "DotNet");
            if (Directory.Exists(dotnetEnginePath))
            {
                string? dotnetVersionFolder = null;
                foreach (var candidateDirectory in Directory.GetDirectories(dotnetEnginePath))
                {
                    if (dotnetVersionFolder == null)
                    {
                        dotnetVersionFolder = Path.GetFileName(candidateDirectory);
                    }
                    else if (string.Compare(Path.GetFileName(candidateDirectory), dotnetVersionFolder, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        dotnetVersionFolder = Path.GetFileName(candidateDirectory);
                    }
                }
                if (dotnetVersionFolder != null)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        dotnetPath = Path.Combine(dotnetEnginePath, dotnetVersionFolder, "windows", "dotnet.exe");
                        if (!File.Exists(dotnetPath))
                        {
                            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                            {
                                dotnetPath = Path.Combine(dotnetEnginePath, dotnetVersionFolder, "win-arm64", "dotnet.exe");
                            }
                            else
                            {
                                dotnetPath = Path.Combine(dotnetEnginePath, dotnetVersionFolder, "win-x64", "dotnet.exe");
                            }
                        }
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        {
                            dotnetPath = Path.Combine(dotnetEnginePath, dotnetVersionFolder, "mac-arm64", "dotnet");
                        }
                        else
                        {
                            dotnetPath = Path.Combine(dotnetEnginePath, dotnetVersionFolder, "mac-x64", "dotnet");
                        }
                    }
                }
            }
            if (dotnetPath != null && !Path.Exists(dotnetPath))
            {
                dotnetPath = null;
            }
            if (dotnetPath == null)
            {
                try
                {
                    dotnetPath = await _pathResolver.ResolveBinaryPath("dotnet").ConfigureAwait(false);
                }
                catch
                {
                }
            }
            if (dotnetPath != null)
            {
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(
                            dotnetPath,
                            File.GetUnixFileMode(dotnetPath) | UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute);
                    }
                    catch
                    {
                    }
                }
            }
            return dotnetPath;
        }
    }
}
