namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Hosting;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker.Endpoints;
    using System.Reflection;
    using System.Runtime.Versioning;
    using Redpoint.Uefs.Package;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;

    public static class UefsDaemonIntegrationDockerServiceExtensions
    {
        public static void AddUefsDaemonIntegrationDocker(this IServiceCollection services)
        {
            // We only run Docker integration on Windows.
            if (OperatingSystem.IsWindows())
            {
                services.AddHostedService<DockerPluginHostedService>();
            }
        }
    }

    [SupportedOSPlatform("windows")]
    internal class DockerPluginHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DockerPluginHostedService> _logger;
        private WebApplication? _app;
        private FileStream? _pluginStream;

        public DockerPluginHostedService(
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            ILogger<DockerPluginHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {   
            // We only run the Docker integration if Docker is installed.
            var dockerRoot = Path.Combine(
                Environment.GetEnvironmentVariable("PROGRAMDATA")!,
                "docker");
            if (!Directory.Exists(dockerRoot))
            {
                return;
            }

            // Create the builder for our Docker socket.
            var builder = WebApplication.CreateBuilder();

            // Forward to the main logger.
            builder.Logging.ClearProviders();
            builder.Logging.Services.AddSingleton(_loggerFactory);

            // Configure this to run on a free port.
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.AllowSynchronousIO = true;

                serverOptions.Listen(
                    new IPEndPoint(IPAddress.Loopback, 0),
                    listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    });
            });
            
            // Map all our endpoints across.
            var endpointHandlerTypes = new List<Type>
            {
                typeof(DockerCapabilitiesEndpointHandler),
                typeof(DockerCreateEndpointHandler),
                typeof(DockerGetEndpointHandler),
                typeof(DockerHandshakeEndpointHandler),
                typeof(DockerListEndpointHandler),
                typeof(DockerMountEndpointHandler),
                typeof(DockerPathEndpointHandler),
                typeof(DockerRemoveEndpointHandler),
                typeof(DockerUnmountEndpointHandler),
            };
            var filteredEndpointHandlerTypes = endpointHandlerTypes
                .Select<Type, (string url, Type type, Func<IServiceProvider, IEndpointHandler> handler)?>(x =>
                {
                    var attribute = x.GetCustomAttribute<EndpointAttribute>();
                    if (attribute == null)
                    {
                        throw new InvalidOperationException($"Endpoint implementation {x.FullName} is missing [Endpoint] attribute.");
                    }
                    var platformAttribute = x.GetCustomAttribute<SupportedOSPlatformAttribute>();
                    if (platformAttribute != null && !OperatingSystem.IsOSPlatform(platformAttribute.PlatformName))
                    {
                        // Exclude this endpoint on this platform.
                        return null;
                    }
                    return (
                        url: attribute.Url,
                        type: x,
                        handler: sp => (IEndpointHandler)sp.GetRequiredService(x));
                })
                .Where(x => x != null)
                .Cast<(string url, Type type, Func<IServiceProvider, IEndpointHandler> handler)>()
                .ToList();

            // Register all of the endpoint handler types in DI so that they can be used by other components.
            foreach (var kv in filteredEndpointHandlerTypes)
            {
                builder.Services.AddTransient(kv.type, kv.handler);
            }

            // For services we know we'll need in our handlers, delegate them to the main
            // service provider.
            builder.Services.AddTransient(_ => _serviceProvider.GetRequiredService<IPackageMounterDetector>());
            builder.Services.AddTransient(_ => _serviceProvider.GetRequiredService<ILogger<DockerMountEndpointHandler>>());

            // Build the web application.
            _app = builder.Build();

            // Now map all of the discovered endpoints to the Kestrel web server.
            var endpointHandlers = filteredEndpointHandlerTypes.Select(x =>
            {
                return new KeyValuePair<string, Func<IServiceProvider, IEndpointHandler>>(
                    x.url,
                    x.handler);
            }).ToDictionary(k => k.Key, v => v.Value);
            foreach (var kv in endpointHandlers)
            {
                _logger.LogInformation($"Mapping endpoint: {kv.Key}");
                _app.MapMethods(kv.Key, new[] { HttpMethods.Get, HttpMethods.Post, HttpMethods.Put }, async context =>
                {
                    var requestString = string.Empty;
                    if (context.Request.Body != null)
                    {
                        using (var reader = new StreamReader(context.Request.Body))
                        {
                            requestString = await reader.ReadToEndAsync();
                        }
                    }

                    int responseCode = 500;
                    string responseString = string.Empty;
                    try
                    {
                        (responseCode, responseString) = await endpointHandlers[context.Request.Path](context.RequestServices).HandleAsync(
                            context.RequestServices.GetRequiredService<IUefsDaemon>(),
                            requestString,
                            DockerJsonSerializerContext.Default);
                    }
                    catch (Exception exx)
                    {
                        _logger.LogError(exx, $"daemon error while handling HTTP request: {exx}");
                        context.Response.StatusCode = 500;
                        using (var writer = new StreamWriter(context.Response.Body))
                        {
                            await writer.WriteAsync(JsonConvert.SerializeObject(new GenericErrorResponse
                            {
                                Err = "internal daemon error, refer to system logs."
                            }));
                        }
                        await context.Response.CompleteAsync();
                        return;
                    }

                    context.Response.StatusCode = responseCode;
                    using (var writer = new StreamWriter(context.Response.Body))
                    {
                        await writer.WriteAsync(responseString);
                    }
                    await context.Response.CompleteAsync();
                    if (context.Request.Path != "/uefs/Poll" || responseCode != 200)
                    {
                        if (responseCode != 200)
                        {
                            _logger.LogError($"{responseCode}: {context.Request.Path}");
                        }
                        else
                        {
                            _logger.LogInformation($"{responseCode}: {context.Request.Path}");
                        }
                    }
                });
            }

            // Start the application.
            await _app.StartAsync();

            // Write out the Docker plugin file.
            var pluginFilePath = Path.Combine(dockerRoot, "plugins", "uefs.json");
            Directory.CreateDirectory(Path.GetDirectoryName(pluginFilePath)!);
            _pluginStream = new FileStream(
                pluginFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete,
                4096,
                FileOptions.DeleteOnClose);
            using (var writer = new StreamWriter(_pluginStream, leaveOpen: true))
            {
                writer.Write($@"{{
    ""Name"": ""uefs"",
    ""Addr"": ""{_app.Urls.First()}""
}}");
                writer.Flush();
            }
            _pluginStream.Flush();

            // Docker integration is now running.
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_pluginStream != null)
            {
                await _pluginStream.DisposeAsync();
                _pluginStream = null;
            }

            if (_app != null)
            {
                await _app.StopAsync();
                _app = null;
            }
        }
    }
}
