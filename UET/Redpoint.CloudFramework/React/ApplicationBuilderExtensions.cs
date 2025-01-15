namespace Redpoint.CloudFramework.React
{
    using global::React.AspNet;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Converters;

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

                // Ensure React initialization uses the same enum encoding as System.Text.Json.
                config.JsonSerializerSettings.Converters.Add(new StringEnumConverter());

                // Do not perform camel-casing automatically.
                config.JsonSerializerSettings.ContractResolver = null;

                var resourceFilter = app.ApplicationServices.GetService<IWebpackResourceFilter>();
                if (resourceFilter != null)
                {
                    config.FilterResource = x => resourceFilter.ShouldIncludeResource(x);
                }
            });
        }
    }
}
