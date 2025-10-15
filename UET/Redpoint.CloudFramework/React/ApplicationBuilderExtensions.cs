namespace Redpoint.CloudFramework.React
{
    using global::React.AspNet;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;

    public static class ApplicationBuilderExtensions
    {
        public static void UseReactAppWithOpenApi(this IApplicationBuilder app, bool enableReact18 = false)
        {
            app.UseSwagger(options =>
            {
                options.RouteTemplate = "/api/{documentName}/openapi.json";
            });

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/api/v1/openapi.json", "v1");
                options.InjectStylesheet("/css/swagger.css");
            });

            app.UseReact(config =>
            {
                config
                    .SetReuseJavaScriptEngines(true)
                    .SetReactAppBuildPath("~/dist");
                if (enableReact18)
                {
                    config.EnableReact18RootAPI();
                }

                var resourceFilter = app.ApplicationServices.GetService<IWebpackResourceFilter>();
                if (resourceFilter != null)
                {
                    config.FilterResource = x => resourceFilter.ShouldIncludeResource(x);
                }
            });
        }
    }
}
