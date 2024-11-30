﻿namespace Redpoint.Uet.SdkManagement
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
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;

    public static class SdkManagementServiceExtensions
    {
        public static void AddSdkManagement(this IServiceCollection services)
        {
            services.AddSingleton<ILocalSdkManager, DefaultLocalSdkManager>();

            services.AddSingleton<IVersionNumberResolver, DefaultVersionNumberResolver>();
            services.AddSingleton<IWindowsVersionNumbers, EmbeddedWindowsVersionNumbers>();
            services.AddSingleton<IWindowsVersionNumbers, JsonWindowsVersionNumbers>();
            services.AddSingleton<IMacVersionNumbers, EmbeddedMacVersionNumbers>();
            services.AddSingleton<IMacVersionNumbers, JsonMacVersionNumbers>();
            services.AddSingleton<ILinuxVersionNumbers, EmbeddedLinuxVersionNumbers>();
            services.AddSingleton<ILinuxVersionNumbers, JsonLinuxVersionNumbers>();
            services.AddSingleton<IAndroidVersionNumbers, EmbeddedAndroidVersionNumbers>();
            services.AddSingleton<IAndroidVersionNumbers, JsonAndroidVersionNumbers>();

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
                    .Where(x => x.StartsWith("UET_PLATFORM_SDK_CONFIG_PATH_", StringComparison.Ordinal)))
                {
                    var platform = environmentVariableName["UET_PLATFORM_SDK_CONFIG_PATH_".Length..];
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
