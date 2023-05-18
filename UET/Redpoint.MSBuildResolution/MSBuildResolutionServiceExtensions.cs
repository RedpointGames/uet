namespace Redpoint.MSBuildResolution
{
    using Microsoft.Extensions.DependencyInjection;

    public static class MSBuildResolutionServiceExtensions
    {
        public static void AddMSBuildPathResolution(this IServiceCollection services)
        {
            services.AddSingleton<IMSBuildPathResolver, DefaultMSBuildPathResolver>();
        }
    }
}