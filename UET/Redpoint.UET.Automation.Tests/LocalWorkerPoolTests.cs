namespace Redpoint.UET.Automation.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Automation.Worker;
    using Redpoint.UET.Core;
    using Redpoint.UET.UAT;
    using System.Diagnostics;
    using Xunit.Abstractions;

    public class LocalWorkerPoolTests
    {
        private readonly ITestOutputHelper _output;

        private const int _timeoutSeconds = 180;

        public LocalWorkerPoolTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public async Task CanSpinUpSingleLocalEditorWorker()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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

        [SkippableFact]
        public async Task CanSpinUpTwoLocalEditorWorkers()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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
            int localWorkerCount = 0;
            int totalLocalWorkerCount = 0;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            var workerPool = await workerPoolFactory.CreateAndStartAsync(
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

        [SkippableFact]
        public async Task CanSpinUpSingleLocalGauntletWorker()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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

            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            var workerPool = await workerPoolFactory.CreateAndStartAsync(
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

        [SkippableFact]
        public async Task CanShutdownWorkerWithFinishedWithWorkersAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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

            var sp = services.BuildServiceProvider();

            var didStart = false;
            var didStop = false;
            string? workerPoolFailed = null;

            var semaphore = new SemaphoreSlim(0);

            var workerPoolFactory = sp.GetRequiredService<IWorkerPoolFactory>();
            var cancellationTokenSource = new CancellationTokenSource();
            IWorkerPool? workerPool = null;
            workerPool = await workerPoolFactory.CreateAndStartAsync(
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

        [SkippableFact]
        public async Task CanShutdownWorkerWithFinishedWithDescriptorAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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
    }
}