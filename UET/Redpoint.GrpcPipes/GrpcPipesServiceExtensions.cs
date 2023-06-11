namespace Redpoint.GrpcPipes
{
    using Microsoft.Extensions.DependencyInjection;

    public static class GrpcPipesServiceExtensions
    {
        public static void AddGrpcPipes(this IServiceCollection services)
        {
            services.AddSingleton<IGrpcPipeFactory, AspNetGrpcPipeFactory>();
        }
    }
}