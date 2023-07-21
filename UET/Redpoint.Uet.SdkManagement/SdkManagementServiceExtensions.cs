namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Core;
    using System.Text.Json;
    using System;
    using System.Text.Json.Serialization;
    using Redpoint.Uet.SdkManagement.AutoSdk;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;

    public static class SdkManagementServiceExtensions
    {
        public static void AddSdkManagement(this IServiceCollection services)
        {
            services.AddSingleton<ISimpleDownloadProgress, SimpleDownloadProgress>();
            services.AddSingleton<ILocalSdkManager, DefaultLocalSdkManager>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<WindowsSdkInstaller, WindowsSdkInstaller>();
                services.AddSingleton<ISdkSetup, WindowsSdkSetup>();
                services.AddSingleton<ISdkSetup, AndroidSdkSetup>();
                services.AddSingleton<ISdkSetup, LinuxSdkSetup>();

                // Register confidential implementations.
                foreach (var environmentVariableName in Environment.GetEnvironmentVariables()
                    .Keys
                    .OfType<string>()
                    .Where(x => x.StartsWith("UET_PLATFORM_SDK_CONFIG_PATH_")))
                {
                    var platform = environmentVariableName.Substring("UET_PLATFORM_SDK_CONFIG_PATH_".Length);
                    var configPath = Environment.GetEnvironmentVariable(environmentVariableName)!;
                    var config = JsonSerializer.Deserialize(
                        File.ReadAllText(configPath),
                        new ConfidentialPlatformJsonSerializerContext(new JsonSerializerOptions
                        {
                            Converters =
                            {
                                new JsonStringEnumConverter(),
                            }
                        }).ConfidentialPlatformConfig)!;
                    services.AddSingleton<ISdkSetup>(sp =>
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            return new ConfidentialSdkSetup(
                                platform,
                                config!,
                                sp.GetRequiredService<IProcessExecutor>(),
                                sp.GetRequiredService<IStringUtilities>(),
                                sp.GetRequiredService<WindowsSdkInstaller>(),
                                sp.GetRequiredService<ILogger<ConfidentialSdkSetup>>());
                        }
                        throw new PlatformNotSupportedException();
                    });
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<ISdkSetup, MacSdkSetup>();
            }
        }
    }
}
