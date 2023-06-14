namespace Redpoint.Uefs.Protocol
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.GrpcPipes;

    /// <summary>
    /// Provides <see cref="AddUefs(IServiceCollection)"/> to add the UEFS gRPC client to dependency injection.
    /// </summary>
    public static class UefsServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="Uefs.UefsClient"/> service with dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add the <see cref="Uefs.UefsClient"/> service to.</param>
        public static void AddUefs(this IServiceCollection services)
        {
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IGrpcPipeFactory>();
                return factory.CreateClient(
                    "UEFS",
                    GrpcPipeNamespace.Computer,
                    channel => new Uefs.UefsClient(channel));
            });
        }
    }
}
