namespace UET.Commands.Internal.InstallPlatformSdk
{
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class InstallPlatformSdkCommand
    {
        public class Options
        {
            public Option<DirectoryInfo> EnginePath;
            public Option<DirectoryInfo> PackagePath;
            public Option<string> Platform;

            public Options()
            {
                EnginePath = new Option<DirectoryInfo>(
                    name: "--engine-path",
                    description: "The path to the Unreal Engine installation.")
                {
                    IsRequired = true,
                };
                PackagePath = new Option<DirectoryInfo>(
                    name: "--sdk-package-path",
                    description: "The path to store generated SDK packages.")
                {
                    IsRequired = true,
                };
                Platform = new Option<string>(
                    name: "--platform",
                    description: "The platform to install SDKs for. Defaults to the host platform.");
            }
        }

        public static Command CreateInstallPlatformSdkCommand()
        {
            var options = new Options();
            var command = new Command("install-platform-sdk");
            command.AddAllOptions(options);
            command.AddCommonHandler<InstallPlatformSdkCommandInstance>(options);
            return command;
        }

        private class InstallPlatformSdkCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILocalSdkManager _localSdkManager;

            public InstallPlatformSdkCommandInstance(
                Options options,
                ILocalSdkManager localSdkManager)
            {
                _options = options;
                _localSdkManager = localSdkManager;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var enginePath = context.ParseResult.GetValueForOption(_options.EnginePath);
                var packagePath = context.ParseResult.GetValueForOption(_options.PackagePath);
                var platform = context.ParseResult.GetValueForOption(_options.Platform);

                if (enginePath == null || !enginePath.Exists)
                {
                    Console.Error.WriteLine("error: --engine-path must be set and exist.");
                    return 1;
                }
                if (packagePath == null)
                {
                    Console.Error.WriteLine("error: --sdk-package-path must be set.");
                    return 1;
                }
                if (!packagePath.Exists)
                {
                    packagePath.Create();
                }
                if (string.IsNullOrWhiteSpace(platform))
                {
                    if (OperatingSystem.IsWindows())
                    {
                        platform = "Windows";
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        platform = "Mac";
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        platform = "Linux";
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }

                var envVars = await _localSdkManager.SetupEnvironmentForBuildGraphNode(
                    enginePath.FullName,
                    packagePath.FullName,
                    // @note: We just leverage the fact that the BuildGraph node name containing
                    // the platform is enough for the platform to be installed.
                    platform,
                    context.GetCancellationToken()).ConfigureAwait(false);

                Console.WriteLine("The following environment variables would be set:");
                foreach (var kv in envVars)
                {
                    Console.WriteLine($"  {kv.Key} = {kv.Value}");
                }
                return 0;
            }
        }
    }
}
