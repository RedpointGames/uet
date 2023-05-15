using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.PathResolution.Tests")]

namespace Redpoint.PathResolution
{
    using Microsoft.Extensions.DependencyInjection;

    public static class PathResolutionServiceExtensions
    {
        public static void AddPathResolution(this IServiceCollection services)
        {
            services.AddSingleton<IPathResolver, DefaultPathResolver>();
        }
    }
}