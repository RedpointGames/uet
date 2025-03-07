/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using JavaScriptEngineSwitcher.Core;
using Microsoft.Extensions.DependencyInjection;

namespace React.AspNet
{
    /// <summary>
    /// Handles registering ReactJS.NET services in the ASP.NET <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ReactServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all services required for ReactJS.NET
        /// </summary>
        /// <param name="services">ASP.NET services</param>
        /// <returns>The service collection (for chaining)</returns>
        public static IServiceCollection AddReact(this IServiceCollection services)
        {
            services.AddSingleton<IReactSiteConfiguration, ReactSiteConfiguration>();
            services.AddScoped<IFileCacheHash, FileCacheHash>();
            services.AddSingleton<IJsEngineSwitcher>(sp => JsEngineSwitcher.Current);
            services.AddSingleton<IJavaScriptEngineFactory, JavaScriptEngineFactory>();
            services.AddSingleton<IReactIdGenerator, ReactIdGenerator>();
            services.AddScoped<IReactEnvironment, ReactEnvironment>();

            services.AddSingleton<IFileSystem, AspNetFileSystem>();
            services.AddSingleton<ICache, MemoryFileCacheCore>();

            return services;
        }
    }
}
