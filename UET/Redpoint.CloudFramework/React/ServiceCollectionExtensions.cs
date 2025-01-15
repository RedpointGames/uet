namespace Redpoint.CloudFramework.React
{
    using global::React.AspNet;
    using JavaScriptEngineSwitcher.Extensions.MsDependencyInjection;
    using JavaScriptEngineSwitcher.V8;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.OpenApi;
    using System.Diagnostics;

    public static class ServiceCollectionExtensions
    {
        public static void AddReactAppWithOpenApi(this IServiceCollection services, IHostEnvironment hostEnvironment)
        {
            // Add React services.
            services.AddJsEngineSwitcher(options => options.DefaultEngineName = V8JsEngine.EngineName)
                .AddV8();
            services.AddReact();            

            services.AddSwaggerGenForReactApp();
            services.AddWebpackDevWatchForReactAppInDevelopment(hostEnvironment);
        }

        public static void AddWebpackDevWatchForReactAppInDevelopment(this IServiceCollection services, IHostEnvironment hostEnvironment)
        {
            if (hostEnvironment.IsDevelopment() && Debugger.IsAttached)
            {
                services.AddSingleton<IHostedService, WebpackDevWatchHostedService>();
            }
        }
    }
}
