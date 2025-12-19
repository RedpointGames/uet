namespace Redpoint.KubernetesManager.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using Redpoint.Concurrency;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifest.Client;
    using Redpoint.KubernetesManager.Services;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using Xunit;

    public class ManifestTests
    {
        private readonly ITestOutputHelper _output;

        public ManifestTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestManifestUpdates()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_output);
            });
            services.AddRkmManifest();

            var client = services.BuildServiceProvider().GetRequiredService<IGenericManifestClient>();

            using var cts = new CancellationTokenSource();

            // Start our client task that polls for updates.
            var updatesReceived = new List<TestManifest>();
            var cancellationsReceived = new List<bool>();
            var clientTask = Task.Run(
                async () => await client.RegisterAndRunWithManifestAsync<TestManifest>(
                    new Uri("ws://127.0.0.1:52934"),
                    null,
                    TestManifestJsonSerializerContext.Default.TestManifest,
                    async (testManifest, cancellationToken) =>
                    {
                        updatesReceived.Add(testManifest);
                        try
                        {
                            await Task.Delay(-1, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationsReceived.Add(true);
                        }
                    },
                    cts.Token),
                cts.Token);

            // Start our local web server.
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:52934/");
            listener.Start();
            try
            {
                var context = await listener.GetContextAsync().AsCancellable(cts.Token);

                if (context.Request.IsWebSocketRequest)
                {
                    var webSocket = await context.AcceptWebSocketAsync(null);
                    await webSocket.WebSocket.SendAsync(
                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestManifest { Value = 1 }, TestManifestJsonSerializerContext.Default.TestManifest)),
                        WebSocketMessageType.Text,
                        true,
                        cts.Token);
                    await Task.Delay(500, TestContext.Current.CancellationToken);
                    Assert.Single(updatesReceived);
                    Assert.Equal(1, updatesReceived[0].Value);
                    Assert.Empty(cancellationsReceived);
                    await webSocket.WebSocket.SendAsync(
                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestManifest { Value = 2 }, TestManifestJsonSerializerContext.Default.TestManifest)),
                        WebSocketMessageType.Text,
                        true,
                        cts.Token);
                    await Task.Delay(500, TestContext.Current.CancellationToken);
                    Assert.Equal(2, updatesReceived.Count);
                    Assert.Equal(2, updatesReceived[1].Value);
                    Assert.Single(cancellationsReceived);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            finally
            {
                listener.Stop();
            }

            cts.Cancel();

            try
            {
                await clientTask;
            }
            catch
            {
            }

            Assert.Equal(2, updatesReceived.Count);
            Assert.Equal(2, updatesReceived[1].Value);
            Assert.Equal(2, cancellationsReceived.Count);
        }
    }
}
