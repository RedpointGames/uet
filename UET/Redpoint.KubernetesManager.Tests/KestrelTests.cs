namespace Redpoint.KubernetesManager.Tests
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.HostedService;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class KestrelTests
    {
        private class RequestHandler : IKestrelRequestHandler
        {
            public async Task HandleRequestAsync(HttpContext httpContext)
            {
                await httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("ok"));
                await httpContext.Response.Body.FlushAsync();
                httpContext.Response.Body.Close();
            }
        }

        [Fact]
        public async Task CanStartKestrel()
        {
            var services = new ServiceCollection();
            services.AddKubernetesManager(false);
            services.AddSingleton<IHostApplicationLifetime, RkmHostApplicationLifetime>();

            var serviceProvider = services.BuildServiceProvider();

            var kestrelFactory = serviceProvider.GetRequiredService<IKestrelFactory>();

            var kestrelServerOptions = new KestrelServerOptions();
            kestrelServerOptions.ListenLocalhost(8080);

            using var kestrel = await kestrelFactory.CreateAndStartServerAsync(
                kestrelServerOptions,
                new RequestHandler(),
                TestContext.Current.CancellationToken);

            using (var client = new HttpClient())
            {
                var result = await client.GetStringAsync(
                    new Uri("http://localhost:8080"),
                    TestContext.Current.CancellationToken);
                Assert.Equal("ok", result);
            }

            await kestrel.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
