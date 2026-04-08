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
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using Redpoint.Uet.SdkManagement.Sdk.Discovery;
    using Redpoint.Uet.SdkManagement.Sdk.MsiExtract;

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

            services.AddSingleton<ISdkSetupDiscovery, DefaultSdkSetupDiscovery>();

            services.AddSingleton<IMsiExtraction, DefaultMsiExtraction>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<WindowsSdkInstaller, WindowsSdkInstaller>();
            }
        }
    }
}
