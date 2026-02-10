namespace Io
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Io.Readers;
    using System;
    using Io.Database;
    using Io.Json.GitLab;
    using Io.Mappers;
    using Io.Redis;
    using StackExchange.Redis;
    using Io.Processor.Periodic;
    using Io.Processor;

    public class Startup
    {
        private readonly IHostEnvironment _hostEnvironment;

        public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            _hostEnvironment = hostEnvironment;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddHealthChecks();

            // Register the reader which is used to grab runner information.
            services.AddReaders();

            // Register our additional database related services.
            services.AddDatabaseServices();

            // Register mappers for converting from GitLab JSON to EF.
            services.AddMappers();

            // Register the Postgres database connection, which is used for log storage.
            services.AddIoDbContext(_hostEnvironment, Configuration);

            services.AddSingleton<ConnectionMultiplexer>(ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_SERVER") ?? "localhost:6379"));
            services.AddSingleton<INotificationHub, RedisNotificationHub>();

            services.AddControllersWithViews();

            services.AddHostedService<ApplyDbMigrationsHostedService>();
            services.AddHostedService<GitLabWebhookConfigurationService>();
            services.AddHostedService<IoDataHubNotificationPropagation>();
            if (!_hostEnvironment.IsProduction())
            {
                services.AddProcessors();
            }

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IoDbContext db)
        {
            // We know we're always behind HTTPS.
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseSentryTracing();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");

                endpoints.MapHub<IoDataHub>("/hub");

                endpoints.MapHealthChecks("/healthz");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}
