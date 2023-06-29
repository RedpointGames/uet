namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.DependencyInjection;

    public static class SdkManagementServiceExtensions
    {
        public static void AddSdkManagement(this IServiceCollection services)
        {
            services.AddSingleton<ISimpleDownloadProgress, SimpleDownloadProgress>();
            services.AddSingleton<ILocalSdkManager, DefaultLocalSdkManager>();
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<WindowsSdkSetup, WindowsSdkSetup>();
                services.AddSingleton<AndroidSdkSetup, AndroidSdkSetup>();
                services.AddSingleton<LinuxSdkSetup, LinuxSdkSetup>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<MacSdkSetup, MacSdkSetup>();
            }
        }
    }
}
