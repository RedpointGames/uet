namespace Redpoint.Uet.Automation.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.Worker;
    using Redpoint.Uet.Uat;
    using System.Diagnostics;

    public class LocalWorkerPoolTests
    {
        private readonly ITestOutputHelper _output;

        private const int _timeoutSeconds = 180;

        public LocalWorkerPoolTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CanSpinUpSingleLocalEditorWorker()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddProcessExecution();
            services.AddUETAutomation();
            services.AddUETUAT();

            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            var workerPool = await workerPoolFactory.CreateAndStartAsync(
                sp.GetRequiredService<ITestLoggerFactory>().CreateConsole(),
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = "Win64",
                        IsEditor = true,
                        Configuration = "Development",
                        Target = "ExampleOSSEditor",
                        UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                        EnginePath = @"E:\EpicGames\UE_5.2",
                        MinWorkerCount = 1,
                        MaxWorkerCount = null,
                        EnableRendering = true,
                    }
                },
                (worker) =>
                {
                    didStart = true;
                    // Ok, now stop.
                    cancellationTokenSource.Cancel();
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (worker, exitCode, crashData) =>
                {
                    didStop = true;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (reason) =>
                {
                    workerPoolFailed = reason;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);
            try
            {
                if (Debugger.IsAttached)
                {
                    await semaphore.WaitAsync();
                }
                else
                {
                    Assert.True(await semaphore.WaitAsync(_timeoutSeconds * 1000), $"Expected to complete in {_timeoutSeconds} seconds");
                }
            }
            finally
            {
                await workerPool.DisposeAsync();
            }

            Assert.True(workerPoolFailed == null, $"Worker pool failure reason: {workerPoolFailed}");
            Assert.True(didStart, "Worker is expected to start");
            Assert.True(didStop, "Worker is expected to stop");
        }

        [Fact]
        public async Task CanSpinUpTwoLocalEditorWorkers()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = CreateServices();
            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            int localWorkerCount = 0;
            int totalLocalWorkerCount = 0;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            var workerPool = await workerPoolFactory.CreateAndStartAsync(
                sp.GetRequiredService<ITestLoggerFactory>().CreateConsole(),
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = "Win64",
                        IsEditor = true,
                        Configuration = "Development",
                        Target = "ExampleOSSEditor",
                        UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                        EnginePath = @"E:\EpicGames\UE_5.2",
                        MinWorkerCount = 2,
                        MaxWorkerCount = null,
                        EnableRendering = true,
                    }
                },
                (worker) =>
                {
                    didStart = true;
                    localWorkerCount++;
                    totalLocalWorkerCount++;
                    if (localWorkerCount == 2)
                    {
                        // Ok, now stop.
                        cancellationTokenSource.Cancel();
                    }
                    return Task.CompletedTask;
                },
                (worker, exitCode, crashData) =>
                {
                    localWorkerCount--;
                    if (localWorkerCount == 0)
                    {
                        didStop = true;
                        semaphore.Release();
                    }
                    return Task.CompletedTask;
                },
                (reason) =>
                {
                    workerPoolFailed = reason;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);
            try
            {
                if (Debugger.IsAttached)
                {
                    await semaphore.WaitAsync();
                }
                else
                {
                    Assert.True(await semaphore.WaitAsync(_timeoutSeconds * 1000), $"Expected to complete in {_timeoutSeconds} seconds");
                }
            }
            finally
            {
                await workerPool.DisposeAsync();
            }

            Assert.True(workerPoolFailed == null, $"Worker pool failure reason: {workerPoolFailed}");
            Assert.True(didStart, "Worker is expected to start");
            Assert.True(didStop, "Worker is expected to stop");
            Assert.Equal(2, totalLocalWorkerCount);
        }

        [Fact]
        public async Task CanSpinUpSingleLocalGauntletWorker()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = CreateServices();
            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            var workerPool = await workerPoolFactory.CreateAndStartAsync(
                sp.GetRequiredService<ITestLoggerFactory>().CreateConsole(),
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = "Win64",
                        IsEditor = false,
                        Configuration = "Development",
                        Target = "ExampleOSS",
                        UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                        EnginePath = @"E:\EpicGames\UE_5.2",
                        MinWorkerCount = 1,
                        MaxWorkerCount = null,
                        EnableRendering = true,
                    }
                },
                (worker) =>
                {
                    didStart = true;
                    // Ok, now stop.
                    cancellationTokenSource.Cancel();
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (worker, exitCode, crashData) =>
                {
                    didStop = true;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (reason) =>
                {
                    workerPoolFailed = reason;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);
            try
            {
                if (Debugger.IsAttached)
                {
                    await semaphore.WaitAsync();
                }
                else
                {
                    Assert.True(await semaphore.WaitAsync(_timeoutSeconds * 1000), $"Expected to complete in {_timeoutSeconds} seconds");
                }
            }
            finally
            {
                await workerPool.DisposeAsync();
            }

            Assert.True(workerPoolFailed == null, $"Worker pool failure reason: {workerPoolFailed}");
            Assert.True(didStart, "Worker is expected to start");
            Assert.True(didStop, "Worker is expected to stop");
        }

        [Fact]
        public async Task CanShutdownWorkerWithFinishedWithWorkersAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = CreateServices();
            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            IWorkerPool? workerPool = null;
            workerPool = await workerPoolFactory.CreateAndStartAsync(
                sp.GetRequiredService<ITestLoggerFactory>().CreateConsole(),
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = "Win64",
                        IsEditor = false,
                        Configuration = "Development",
                        Target = "ExampleOSS",
                        UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                        EnginePath = @"E:\EpicGames\UE_5.2",
                        MinWorkerCount = 1,
                        MaxWorkerCount = null,
                        EnableRendering = true,
                    }
                },
                (worker) =>
                {
                    didStart = true;
                    // Ok, now stop.
                    workerPool!.FinishedWithWorker(worker);
                    return Task.CompletedTask;
                },
                (worker, exitCode, crashData) =>
                {
                    didStop = true;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (reason) =>
                {
                    workerPoolFailed = reason;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);
            try
            {
                if (Debugger.IsAttached)
                {
                    await semaphore.WaitAsync();
                }
                else
                {
                    Assert.True(await semaphore.WaitAsync(_timeoutSeconds * 1000), $"Expected to complete in {_timeoutSeconds} seconds");
                }
            }
            finally
            {
                await workerPool.DisposeAsync();
            }

            Assert.True(workerPoolFailed == null, $"Worker pool failure reason: {workerPoolFailed}");
            Assert.True(didStart, "Worker is expected to start");
            Assert.True(didStop, "Worker is expected to stop");
        }

        [Fact]
        public async Task CanShutdownWorkerWithFinishedWithDescriptorAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = CreateServices();
            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var descriptor = new DesiredWorkerDescriptor
            {
                Platform = "Win64",
                IsEditor = false,
                Configuration = "Development",
                Target = "ExampleOSS",
                UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                EnginePath = @"E:\EpicGames\UE_5.2",
                MinWorkerCount = 1,
                MaxWorkerCount = null,
                EnableRendering = true,
            };

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            IWorkerPool? workerPool = null;
            workerPool = await workerPoolFactory.CreateAndStartAsync(
                sp.GetRequiredService<ITestLoggerFactory>().CreateConsole(),
                new[] { descriptor },
                (worker) =>
                {
                    didStart = true;
                    // Ok, now stop.
                    workerPool!.FinishedWithDescriptor(descriptor);
                    return Task.CompletedTask;
                },
                (worker, exitCode, crashData) =>
                {
                    didStop = true;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                (reason) =>
                {
                    workerPoolFailed = reason;
                    semaphore.Release();
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);
            try
            {
                if (Debugger.IsAttached)
                {
                    await semaphore.WaitAsync();
                }
                else
                {
                    Assert.True(await semaphore.WaitAsync(_timeoutSeconds * 1000), $"Expected to complete in {_timeoutSeconds} seconds");
                }
            }
            finally
            {
                await workerPool.DisposeAsync();
            }

            Assert.True(workerPoolFailed == null, $"Worker pool failure reason: {workerPoolFailed}");
            Assert.True(didStart, "Worker is expected to start");
            Assert.True(didStop, "Worker is expected to stop");
        }

        private IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                    });
            });
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUETAutomation();
            services.AddUETUAT();
            services.AddReservation();
            return services;
        }
    }
}