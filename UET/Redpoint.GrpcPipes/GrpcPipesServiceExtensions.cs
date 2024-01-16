namespace Redpoint.GrpcPipes
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Provides <see cref="AddGrpcPipes(IServiceCollection)"/> to add the gRPC pipes library to dependency injection.
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
            services.AddSingleton<IRetryableGrpc, DefaultRetryableGrpc>();
        }

        /// <summary>
        /// Registers the <see cref="IGrpcPipeFactory"/> service with dependency injection, using the specified factory.
        /// </summary>
        /// <param name="services">The service collection to add the <see cref="IGrpcPipeFactory"/> service to.</param>
        public static void AddGrpcPipes<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(this IServiceCollection services) where TFactory : class, IGrpcPipeFactory
        {
            services.AddSingleton<IGrpcPipeFactory, TFactory>();
            services.AddSingleton<IRetryableGrpc, DefaultRetryableGrpc>();
        }
    }
}