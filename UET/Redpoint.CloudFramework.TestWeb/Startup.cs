namespace Redpoint.CloudFramework.TestWeb
{
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Redpoint.CloudFramework.Tracing;
    using System.Diagnostics.CodeAnalysis;

    [SuppressMessage("Maintainability", "CA1724", Justification = "This is test code.")]
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment() || env.IsStaging())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
                    var managedTracer = context.RequestServices.GetRequiredService<IManagedTracer>();

                    using (var span = managedTracer.StartSpan("test-web-span"))
                    {
                        logger.LogInformation("This is an informational message.");
                    }

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.Headers.ContentType = "text/plain";

                    using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
                    await writer.WriteLineAsync("Hello world!");
                });

                endpoints.MapGet("/error", async context =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();

                    logger.LogError("This is an error message.");

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.Headers.ContentType = "text/plain";

                    using var writer = new StreamWriter(context.Response.Body, leaveOpen: true);
                    await writer.WriteLineAsync("Hello world!");
                });

                endpoints.MapGet("/exception", async context =>
                {
                    throw new InvalidOperationException("The /exception endpoint was hit.");
                });
            });
        }
    }
}
