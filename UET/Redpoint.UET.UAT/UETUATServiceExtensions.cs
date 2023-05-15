namespace Redpoint.UET.UAT
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.UAT.Internal;

    public static class UETUATServiceExtensions
    {
        public static void AddUETUAT(this IServiceCollection services)
        {
            services.AddSingleton<IBuildConfigurationManager, DefaultBuildConfigurationManager>();
            services.AddSingleton<ILocalHandleCloser, DefaultLocalHandleCloser>();
            services.AddSingleton<IRemoteHandleCloser, DefaultRemoteHandleCloser>();

            services.AddSingleton<IUATExecutor, DefaultUATExecutor>();
        }
    }
}