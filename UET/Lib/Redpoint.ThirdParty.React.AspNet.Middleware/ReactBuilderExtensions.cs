/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace React.AspNet
{
    /// <summary>
    /// Handles registering ReactJS.NET middleware in an ASP.NET <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class ReactBuilderExtensions
    {
        /// <summary>
        /// Initialises ReactJS.NET for this application
        /// </summary>
        /// <param name="app">ASP.NET application builder</param>
        /// <param name="configure">ReactJS.NET configuration</param>
        /// <returns>The application builder (for chaining)</returns>
        public static IApplicationBuilder UseReact(
            this IApplicationBuilder app,
            Action<IReactSiteConfiguration> configure
        )
        {
            // Apply configuration.
            configure(app.ApplicationServices.GetRequiredService<IReactSiteConfiguration>());

            return app;
        }
    }
}
