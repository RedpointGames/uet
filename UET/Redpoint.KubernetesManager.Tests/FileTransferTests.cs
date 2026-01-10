namespace Redpoint.KubernetesManager.Tests
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.ProgressMonitor;
    using Redpoint.XunitFramework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class FileTransferTests
    {
        internal class FileTransferHandler : IKestrelRequestHandler
        {
            private readonly IFileTransferServer _fileTransferServer;
            private Func<Stream, Stream> _wrapReadStream;
            private int _proxyCount;

            public SemaphoreSlim Semaphore { get; }

            public Func<Stream, Stream> WrapReadStream
            {
                get => _wrapReadStream;
                set
                {
                    _wrapReadStream = value;
                    ((DefaultFileTransferServer)_fileTransferServer)._wrapReadStream = _wrapReadStream;
                }
            }

            public FileTransferHandler(
                IServiceProvider serviceProvider,
                TimeSpan? stallAfter)
            {
                _fileTransferServer = serviceProvider.GetRequiredService<IFileTransferServer>();

                _wrapReadStream = (stream) =>
                {
                    if (_proxyCount++ < 2)
                    {
                        return new ReadThrottlingStream(stream, (100 * 1024 * 1024) / 10, stallAfter);
                    }
                    else
                    {
                        return stream;
                    }
                };
                ((DefaultFileTransferServer)_fileTransferServer)._wrapReadStream = _wrapReadStream;

                Semaphore = new SemaphoreSlim(0);
            }

            public async Task HandleRequestAsync(HttpContext httpContext)
            {
                await Task.Delay(1000, httpContext.RequestAborted);

                await _fileTransferServer.HandleUploadFileAsync(
                    httpContext,
                    Path.GetTempFileName());
                Semaphore.Release();
            }
        }

        [Fact]
        public async Task CanTransferFileLongerThanFiveSeconds()
        {
            await RunTestInternal(null);
        }

        [Fact]
        public async Task CanDetectStall()
        {
            await RunTestInternal(TimeSpan.FromSeconds(3));
        }

        private static async Task RunTestInternal(TimeSpan? stallAfter)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddXUnit();
            });
            services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
            services.AddKestrelFactory();
            services.AddSingleton<IDurableOperation, DefaultDurableOperation>();
            services.AddSingleton<IFileTransferClient, DefaultFileTransferClient>();
            services.AddSingleton<IFileTransferServer, DefaultFileTransferServer>();
            services.AddProgressMonitor();

            var serviceProvider = services.BuildServiceProvider();

            var httpPort = 8790;

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ApplicationServices = serviceProvider;
            kestrelOptions.Limits.MaxRequestBodySize = null;
            kestrelOptions.Listen(IPAddress.Loopback, httpPort);

            var handler = new FileTransferHandler(
                serviceProvider,
                stallAfter);

            using var kestrelServer = await serviceProvider.GetRequiredService<IKestrelFactory>().CreateAndStartServerAsync(
                kestrelOptions,
                handler,
                TestContext.Current.CancellationToken);
            try
            {
                var tempFile = Path.GetTempFileName();
                using (var writeStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = RandomNumberGenerator.GetBytes(1024);
                    for (int i = 0; i < 100 * 1024; i++)
                    {
                        writeStream.Write(bytes);
                    }
                }

                var fileTransferClient = serviceProvider.GetRequiredService<IFileTransferClient>();
                ((DefaultFileTransferClient)fileTransferClient)._wrapReadStream = handler.WrapReadStream;
                using (var client = new HttpClient())
                {
                    await fileTransferClient.UploadFileAsync(
                        tempFile,
                        new Uri($"http://127.0.0.1:{httpPort}"),
                        client,
                        TestContext.Current.CancellationToken);
                }

                await handler.Semaphore.WaitAsync(
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                await kestrelServer.StopAsync(TestContext.Current.CancellationToken);
            }
        }
    }
}
