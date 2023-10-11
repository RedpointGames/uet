using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.PathResolution.Tests")]

namespace Redpoint.PathResolution
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides registration functions to register an implementation of <see cref="IPathResolver"/> into a <see cref="IServiceCollection"/>.
    /// </summary>
    public static class PathResolutionServiceExtensions
    {
        /// <summary>
        /// Add path resolution services (the <see cref="IPathResolver"/> service) into the service collection.
        /// </summary>
        /// <param name="services">The service collection to register an implementation of <see cref="IPathResolver"/> to.</param>
        public static void AddPathResolution(this IServiceCollection services)
        {
            services.AddSingleton<IPathResolver, DefaultPathResolver>();
        }
    }
}