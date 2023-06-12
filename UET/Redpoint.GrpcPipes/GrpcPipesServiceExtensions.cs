namespace Redpoint.GrpcPipes
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides <see cref="AddGrpcPipes(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/> to add the gRPC pipes library to dependency injection.
    /// </summary>
    public static class GrpcPipesServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IGrpcPipeFactory"/> service with dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add the <see cref="IGrpcPipeFactory"/> service to.</param>
        public static void AddGrpcPipes(this IServiceCollection services)
        {
            services.AddSingleton<IGrpcPipeFactory, AspNetGrpcPipeFactory>();
        }
    }
}