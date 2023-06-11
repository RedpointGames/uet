namespace Redpoint.GrpcPipes
{
    using Microsoft.Extensions.DependencyInjection;

    public static class GrpcPipesServiceExtensions
    {
        public static void AddGrpcPipes(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IGrpcPipeFactory, WindowsGrpcPipeFactory>();
            }
            else if (OperatingSystem.IsMacOS() ||
                OperatingSystem.IsLinux())
            {
                services.AddSingleton<IGrpcPipeFactory, UnixGrpcPipeFactory>();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}