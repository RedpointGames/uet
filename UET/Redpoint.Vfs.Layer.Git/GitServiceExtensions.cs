﻿namespace Redpoint.Vfs.Layer.Git
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers the <see cref="IGitVfsLayerFactory"/> implementation with dependency injection.
    /// </summary>
    public static class GitServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IGitVfsLayerFactory"/> implementation with dependency injection.
        /// </summary>
        public static void AddGitLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IGitVfsLayerFactory, GitVfsLayerFactory>();
        }
    }
}
