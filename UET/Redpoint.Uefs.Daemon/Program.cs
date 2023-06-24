namespace Redpoint.Uefs.Daemon
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Vfs.Driver.WinFsp;
    using Redpoint.Vfs.Layer.Folder;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.Layer.GitDependencies;
    using Redpoint.Vfs.Layer.Scratch;
    using Sentry;
    using Redpoint.Uefs.Daemon.PackageFs;
    using Redpoint.Uefs.Daemon.PackageStorage;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Daemon.Transactional;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Daemon.Service;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Logging.SingleLine;

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using (SentrySdk.Init(o =>
            {
                o.Dsn = "https://2be4aea5d6d14a84b5815d28fc891f53@sentry.redpoint.games/3";
                o.IsGlobalModeEnabled = false;
                o.SendDefaultPii = false;
                o.TracesSampleRate = 1.0;
            }))
            {
                // Create the builder.
                var builder = Host.CreateDefaultBuilder(args);

                // Do required initialization when we're running as a Windows service.
                if (OperatingSystem.IsWindows() && args.Contains("--service"))
                {
                    builder.UseWindowsService(options =>
                    {
                        options.ServiceName = "UEFS Service";
                    });
                }

                // Bind services.
                builder.ConfigureServices(services =>
                {
                    services.AddAllServices(args);
                });

                // Now build and run the host.
                var host = builder.Build();
                await host.RunAsync();
                return 0;
            }
        }

        public static void AddAllServices(this IServiceCollection services, string[] args)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddSingleLineConsoleFormatter();
                logging.AddSingleLineConsole();

                logging.AddSentry(o =>
                {
                    o.Dsn = "https://2be4aea5d6d14a84b5815d28fc891f53@sentry.redpoint.games/3";
                    o.IsGlobalModeEnabled = false;
                    o.SendDefaultPii = false;
                    o.TracesSampleRate = 1.0;
                });

                if (OperatingSystem.IsWindows() && args.Contains("--service"))
                {
                    logging.AddEventLog(settings =>
                    {
#pragma warning disable CA1416
                        settings.SourceName = "UEFS";
#pragma warning restore CA1416
                    });
                }
            });
            services.AddGrpcPipes();
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddWinFspVfsDriver();
            }
            services.AddGitLayerFactory();
            services.AddGitDependenciesLayerFactory();
            services.AddFolderLayerFactory();
            services.AddScratchLayerFactory();
            services.AddGrpcPipes();
            services.AddUefsPackage();
            services.AddUefsPackageFs();
            services.AddUefsPackageStorage();
            services.AddUefsRemoteStorage();
            services.AddUefsService();
            services.AddUefsDaemonTransactional();
            services.AddUefsDaemonTransactionalExecutors();
            services.AddUefsDaemonIntegrationDocker();
            services.AddSingleton<UefsHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<UefsHostedService>());
            services.AddTransient(sp => sp.GetRequiredService<UefsHostedService>().UefsDaemon);
            services.AddTransient<IMountTracking>(sp => sp.GetRequiredService<IUefsDaemon>());
        }

#if FALSE
        private static async Task<int> SafeMain(string[] args)
        {


            // Find a few free TCP ports.
            var dockerTcp = new TcpListener(IPAddress.Any, 0);
            dockerTcp.Start();
            var kubernetesTcp = new TcpListener(IPAddress.Any, 0);
            kubernetesTcp.Start();
            var port = ((IPEndPoint)dockerTcp.LocalEndpoint).Port;
            var http2Port = ((IPEndPoint)kubernetesTcp.LocalEndpoint).Port;
            kubernetesTcp.Stop();
            dockerTcp.Stop();

            // Determine if we're enabling Docker or Kubernetes features.
            var enableDocker = true; // @todo: Docker is always currently enabled.
            var enableKubernetes = false; // @todo: Make this work with things other than RKM.
            string? kubernetesSocketPath = null;
            if (File.Exists(@"C:\RKM\active"))
            {
                var installationId = (await File.ReadAllTextAsync(@"C:\RKM\active")).Trim();
                if (Directory.Exists(@$"C:\RKM\{installationId}\kubernetes-node\state\plugins_registry"))
                {
                    enableKubernetes = true;
                    kubernetesSocketPath = @$"C:\RKM\{installationId}\kubernetes-node\state\plugins_registry\uefs.redpoint.games-reg.sock";
                }
            }

            // Configure the listening ports.
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Our HTTP endpoints use this via the StreamWriter's Dispose method.
                options.AllowSynchronousIO = true;

                options.ListenLocalhost(port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                });
                options.ListenLocalhost(http2Port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });
            if (enableKubernetes)
            {
                builder.Services.AddSingleton(sp => new KubernetesForwardingArguments
                {
                    Port = http2Port,
                    UnixSocketPath = kubernetesSocketPath!,
                });
                builder.Services.AddHostedService<KubernetesProxyWorker>();
            }

            // Map required services.
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                builder.Services.AddWinFspVfsDriver();
            }
            builder.Services.AddGitLayerFactory();
            builder.Services.AddGitDependenciesLayerFactory();
            builder.Services.AddFolderLayerFactory();
            builder.Services.AddScratchLayerFactory();
            builder.Services.AddGrpcPipes();
            builder.Services.AddSingleton<IUefsDaemon, UEFSDaemon>();
            builder.Services.AddTransient<UEFSService, UEFSService>();
            if (enableKubernetes)
            {
                builder.Services.AddGrpc();
            }
            builder.Services.AddHostedService<UEFSNamedPipeHostedService>();

            // Create the storage directory if needed.
            Directory.CreateDirectory(UEFSEnvironment.StoragePath);

            // Write out the plugin file that tells clients where to find
            // the localhost HTTP server.
            File.WriteAllText(UEFSEnvironment.HostJsonPath, );

            // Load all of the Docker and UEFS client endpoint handlers.
            var endpointHandlerTypes = new List<Type>
            {
                typeof(UEFSMountEndpointHandler),
                typeof(UEFSUnmountEndpointHandler),
                typeof(UEFSListEndpointHandler),
                typeof(UEFSPullEndpointHandler),
                typeof(UEFSPollEndpointHandler),
                typeof(UEFSVerifyEndpointHandler),
                typeof(UEFSGetPollsEndpointHandler),
                typeof(UEFSGitFetchEndpointHandler),
            };
            if (enableDocker)
            {
                endpointHandlerTypes.AddRange(new[]
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
                });
            }
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
                builder.Services.AddTransient(kv.type, kv.type);
            }

            // Create the web server.
            var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<IUefsDaemon>>();

            // Now map all of the discovered endpoints to the Kestrel web server.
            var endpointHandlers = filteredEndpointHandlerTypes.Select(x =>
            {
                return new KeyValuePair<string, Func<IServiceProvider, IEndpointHandler>>(
                    x.url,
                    x.handler);
            }).ToDictionary(k => k.Key, v => v.Value);
            foreach (var kv in endpointHandlers)
            {
                // @todo: Figure out how to limit these endpoint registrations to the HTTP1 localhost port.
                logger.LogInformation($"Mapping endpoint: {kv.Key}");
                app.MapMethods(kv.Key, new[] { HttpMethods.Get, HttpMethods.Post, HttpMethods.Put }, async context =>
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
                            requestString);
                    }
                    catch (EndpointException ee)
                    {
                        logger.LogError(ee, $"endpoint error while handling HTTP request: {JsonConvert.SerializeObject(ee.ErrorResponse)}");
                        context.Response.StatusCode = ee.StatusCode;
                        using (var writer = new StreamWriter(context.Response.Body))
                        {
                            await writer.WriteAsync(JsonConvert.SerializeObject(ee.ErrorResponse));
                        }
                        await context.Response.CompleteAsync();
                        return;
                    }
                    catch (Exception exx)
                    {
                        logger.LogError(exx, $"daemon error while handling HTTP request: {exx}");
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
                            logger.LogError($"{responseCode}: {context.Request.Path}");
                        }
                        else
                        {
                            logger.LogInformation($"{responseCode}: {context.Request.Path}");
                        }
                    }
                });
            }

            if (enableKubernetes)
            {
                // Map the gRPC services for Kubernetes.
                app.MapGrpcService<IdentityService>();
                app.MapGrpcService<NodeService>();
                app.MapGrpcService<RegistrationService>();
            }

            // Now initialize and run the UEFS daemon.
            try
            {
                var daemon = app.Services.GetRequiredService<IUefsDaemon>();
                await daemon.InitAsync();

                if (enableDocker)
                {
                    logger.LogInformation("Docker support is enabled.");
                }
                if (enableKubernetes)
                {
                    logger.LogInformation($"Kubernetes support is enabled. Socket path: {kubernetesSocketPath}");
                }

                logger.LogInformation($"uefs daemon is now running on port {port} (HTTP 1) and {http2Port} (HTTP 2). Press Ctrl-C to stop.");
                await app.RunAsync();
                logger.LogInformation($"uefs daemon is now shutting down, please wait.");
            }
            finally
            {
                logger.LogInformation($"uefs daemon is now cleaning up local drive paths...");
                PathUtils.CleanupDriveLocalPaths();
                logger.LogInformation($"uefs daemon has finished cleaning up local drive paths.");
            }

            return 0;
#endif
    }
}

