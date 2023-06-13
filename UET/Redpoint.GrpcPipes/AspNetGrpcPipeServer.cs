namespace Redpoint.GrpcPipes
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics.CodeAnalysis;

    internal class AspNetGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T> where T : class
    {
        private readonly WebApplication _app;

        public AspNetGrpcPipeServer(
            string unixSocketPath,
            T instance)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(unixSocketPath)!);
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();
            builder.Services.Add(new ServiceDescriptor(
                typeof(T),
                instance));
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenUnixSocket(
                    unixSocketPath,
                    listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
            });

            var app = builder.Build();
            app.UseRouting();
            app.MapGrpcService<T>();

            _app = app;
        }

        public Task StartAsync()
        {
            return _app.StartAsync();
        }

        public Task StopAsync()
        {
            return _app.StopAsync();
        }
    }
}