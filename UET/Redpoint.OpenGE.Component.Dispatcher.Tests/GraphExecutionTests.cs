namespace Redpoint.OpenGE.Component.Dispatcher.Tests
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.Collections;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.GrpcPipes.Transport.Tcp;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Component.Worker;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.JobXml;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Tasks;
    using System.Collections.Concurrent;
    using System.Net;
    using Xunit;
    using Xunit.Abstractions;

    public partial class GraphExecutionTests
    {
        private readonly ITestOutputHelper _output;

        public GraphExecutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed class TestPreprocessorCacheAccessor : IPreprocessorCacheAccessor
        {
            private readonly IPreprocessorCache _preprocessorCache;

            public TestPreprocessorCacheAccessor(IPreprocessorCache preprocessorCache)
            {
                _preprocessorCache = preprocessorCache;
            }

            public Task<IPreprocessorCache> GetPreprocessorCacheAsync()
            {
                return Task.FromResult<IPreprocessorCache>(_preprocessorCache);
            }
        }

        [SkippableFact]
        public async Task ExecutionGraphBasicTest()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddTasks();
            services.AddLogging();
            services.AddGrpcPipes<TcpGrpcPipeFactory>();
            services.AddOpenGECore();
            services.AddOpenGEComponentDispatcher();
            services.AddOpenGEComponentWorker();
            services.AddOpenGEComponentPreprocessorCache();
            services.AddProcessExecution();
            services.AddReservation();
            services.AddAutoDiscovery();
            services.AddSingleton<IPreprocessorCacheAccessor, TestPreprocessorCacheAccessor>();
            // @note: We should really have an "InMemory" cache for unit tests like this, 
            // but we know this will never actually get used at runtime.
            services.AddSingleton<IPreprocessorCache>(sp => sp.GetRequiredService<IPreprocessorCacheFactory>().CreateInProcessCache());
            var provider = services.BuildServiceProvider();

            var grpcPipeFactory = provider.GetRequiredService<IGrpcPipeFactory>();
            var workerFactory = provider.GetRequiredService<IWorkerComponentFactory>();
            var dispatcherFactory = provider.GetRequiredService<IDispatcherComponentFactory>();
            var workerPoolFactory = provider.GetRequiredService<ITaskApiWorkerPoolFactory>();

            var worker = workerFactory.Create(true);
            await worker.StartAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var workerClient = grpcPipeFactory.CreateNetworkClient(
                    new IPEndPoint(IPAddress.Loopback, worker.ListeningPort!.Value),
                    x => new TaskApi.TaskApiClient(x));
                await using (workerPoolFactory.CreateWorkerPool(new TaskApiWorkerPoolConfiguration
                {
                    EnableNetworkAutoDiscovery = false,
                    LocalWorker = new TaskApiWorkerPoolConfigurationLocalWorker
                    {
                        DisplayName = "Test Client",
                        UniqueId = "1",
                        Client = workerClient,
                    }
                }).AsAsyncDisposable(out var workerPool).ConfigureAwait(false))
                {
                    var dispatcher = dispatcherFactory.Create(
                        workerPool,
                        null);
                    await dispatcher.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        var dispatcherClient = grpcPipeFactory.CreateClient(
                            dispatcher.GetConnectionString(),
                            GrpcPipeNamespace.User,
                            channel => new JobApi.JobApiClient(channel));
                        var jobResults = dispatcherClient.SubmitJob(new SubmitJobRequest
                        {
                            JobXml =
                            """
                            <BuildSet FormatVersion="1">
                              <Environments>
                                <Environment Name="Env_0">
                                  <Tools>
                                    <Tool Name="Tool1_0" AllowRemote="True" GroupPrefix="Test1" Params="/C echo ok1" Path="C:\Windows\system32\cmd.exe" />
                                    <Tool Name="Tool2_0" AllowRemote="True" GroupPrefix="Test2" Params="/C echo ok2" Path="C:\Windows\system32\cmd.exe" />
                                  </Tools>
                                </Environment>
                              </Environments>
                              <Project Name="Env_0" Env="Env_0">
                                <Task SourceFile="" Caption="Test1" Name="Action1_0" Tool="Tool1_0" WorkingDir="C:\Windows\system32" SkipIfProjectFailed="true" />
                                <Task SourceFile="" Caption="Test2" Name="Action2_0" Tool="Tool2_0" WorkingDir="C:\Windows\system32" SkipIfProjectFailed="true" DependsOn="Action1_0" />
                              </Project>
                            </BuildSet>
                            """,
                            WorkingDirectory = @"C:\Windows\system32",
                            BuildNodeName = "WorkerPoolTests",
                        });
                        var messages = new List<JobResponse>();
                        await foreach (var message in jobResults.ResponseStream.ReadAllAsync())
                        {
                            messages.Add(message);
                        }
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.JobParsed &&
                            x.JobParsed.TotalTasks == 2);
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskStarted &&
                            x.TaskStarted.Id == "Action1_0");
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskOutput &&
                            x.TaskOutput.Id == "Action1_0" &&
                            x.TaskOutput.OutputCase == TaskOutputResponse.OutputOneofCase.StandardOutputLine &&
                            x.TaskOutput.StandardOutputLine.Trim() == "ok1");
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskCompleted &&
                            x.TaskCompleted.Id == "Action1_0" &&
                            x.TaskCompleted.Status == TaskCompletionStatus.TaskCompletionSuccess &&
                            x.TaskCompleted.ExitCode == 0);
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskStarted &&
                            x.TaskStarted.Id == "Action2_0");
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskOutput &&
                            x.TaskOutput.Id == "Action2_0" &&
                            x.TaskOutput.OutputCase == TaskOutputResponse.OutputOneofCase.StandardOutputLine &&
                            x.TaskOutput.StandardOutputLine.Trim() == "ok2");
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.TaskCompleted &&
                            x.TaskCompleted.Id == "Action2_0" &&
                            x.TaskCompleted.Status == TaskCompletionStatus.TaskCompletionSuccess &&
                            x.TaskCompleted.ExitCode == 0);
                        Assert.Contains(messages, x =>
                            x.ResponseCase == JobResponse.ResponseOneofCase.JobComplete &&
                            x.JobComplete.Status == JobCompletionStatus.JobCompletionSuccess);
                        Assert.NotEmpty(messages);
                    }
                    finally
                    {
                        await dispatcher.StopAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await worker.StopAsync().ConfigureAwait(false);
            }
        }

        private static Graph CreateGraphForJobCount(
            ITaskDescriptorFactory taskDescriptorFactory,
            int jobCount)
        {
            var job = new Job
            {
                Environments = new Dictionary<string, JobEnvironment>
                    {
                        {
                            "Test",
                            new JobEnvironment
                            {
                                Name = $"Test",
                                Tools = new Dictionary<string, JobTool>
                                {
                                    {
                                        "Test",
                                        new JobTool
                                        {
                                            Name = "Test",
                                            AutoRecover = null,
                                            Params = string.Empty,
                                            Path = "__openge_unit_testing__",
                                        }
                                    }
                                },
                                Variables = new Dictionary<string, string>(),
                            }
                        }
                    },
                Projects = new Dictionary<string, JobProject>(),
            };
            var project = new JobProject
            {
                Name = "Test",
                Env = "Test",
                Tasks = Enumerable.Range(0, jobCount).Select(x => new JobTask
                {
                    Name = $"Test{x}",
                    Caption = $"Test{x}",
                    DependsOn = null,
                    SkipIfProjectFailed = false,
                    Tool = $"Test{x}",
                    WorkingDir = string.Empty,
                }).ToDictionary(k => k.Name, v => v),
            };
            job.Projects.Add("Test", project);
            var tasks = new Dictionary<string, GraphTask>();
            var dependencies = new DependencyGraph<GraphTask>();
            for (int x = 0; x < jobCount; x++)
            {
                var task = new FastExecutableGraphTask
                {
                    GraphTaskSpec = new GraphTaskSpec
                    {
                        Arguments = Array.Empty<EscapedProcessArgument>(),
                        Environment = job.Environments["Test"],
                        ExecutionEnvironment = new GraphExecutionEnvironment
                        {
                            BuildStartTicks = 0,
                            EnvironmentVariables = new Dictionary<string, string>(),
                            WorkingDirectory = string.Empty,
                        },
                        Job = job,
                        Project = project,
                        Task = project.Tasks[$"Test{x}"],
                        Tool = job.Environments["Test"].Tools["Test"],
                    },
                    TaskDescriptorFactory = taskDescriptorFactory,
                };
                tasks.Add($"Test{x}", task);
                dependencies.SetDependsOn(task, Array.Empty<GraphTask>());
            }
            var graph = new Graph
            {
                Projects = new Dictionary<string, JobProject>
                    {
                        {
                            "Test",
                            project
                        }
                    },
                Tasks = tasks,
                TaskDependencies = dependencies,
                ImmediatelySchedulable = tasks.Values.ToList(),
            };
            return graph;
        }

        [Theory]
        [InlineData(new object[] { 10, 1000 })]
#if FALSE
        [InlineData(new object[] { 100, 10000 })]
        [InlineData(new object[] { 1000, 20000 })]
        [InlineData(new object[] { 2000, 20000 })]
        [InlineData(new object[] { 8000, 30000 })]
        [InlineData(new object[] { 20000, 60000 })]
#endif
        public async Task ExecutionGraphRunsWithJobCount(int jobCount, int testTimeoutMilliseconds)
        {
            var cancellationToken = new CancellationTokenSource(testTimeoutMilliseconds).Token;

            var services = new ServiceCollection();
            services.AddTasks();
            services.AddLogging(builder =>
            {
                if (Environment.GetEnvironmentVariable("CI") != "true")
                {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddXUnit(
                        _output,
                        configure =>
                        {
                        });
                }
            });
            services.AddGrpcPipes<TcpGrpcPipeFactory>();
            services.AddOpenGECore();
            services.AddOpenGEComponentDispatcher();
            services.AddOpenGEComponentWorker();
            services.AddOpenGEComponentPreprocessorCache();
            services.AddProcessExecution();
            services.AddReservation();
            services.AddAutoDiscovery();
            services.AddSingleton<IPreprocessorCacheAccessor, TestPreprocessorCacheAccessor>();
            // @note: We should really have an "InMemory" cache for unit tests like this, 
            // but we know this will never actually get used at runtime.
            services.AddSingleton<IPreprocessorCache>(sp => sp.GetRequiredService<IPreprocessorCacheFactory>().CreateInProcessCache());
            var provider = services.BuildServiceProvider();

            var executor = provider.GetRequiredService<IGraphExecutor>();
            var grpcPipeFactory = provider.GetRequiredService<IGrpcPipeFactory>();
            var workerPoolFactory = provider.GetRequiredService<ITaskApiWorkerPoolFactory>();
            var workerFactory = provider.GetRequiredService<IWorkerComponentFactory>();
            var taskDescriptorFactory = provider.GetRequiredService<LocalTaskDescriptorFactory>();

            var logger = provider.GetRequiredService<ILogger<IGraphExecutor>>();

            var worker = workerFactory.Create(true);
            await worker.StartAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var workerClient = grpcPipeFactory.CreateNetworkClient(
                    new IPEndPoint(IPAddress.Loopback, worker.ListeningPort!.Value),
                    x => new TaskApi.TaskApiClient(x));

                var workerPool = workerPoolFactory.CreateWorkerPool(new TaskApiWorkerPoolConfiguration
                {
                    EnableNetworkAutoDiscovery = false,
                    LocalWorker = new TaskApiWorkerPoolConfigurationLocalWorker
                    {
                        DisplayName = "Test Client",
                        UniqueId = "1",
                        Client = workerClient,
                    }
                });
                if (Environment.GetEnvironmentVariable("CI") != "true")
                {
                    workerPool.SetTracer(new LoggingWorkerPoolTracer(logger));
                }

                var nullResponseStream = new NullGuardedResponseStream<JobResponse>();
                var graph = CreateGraphForJobCount(
                    taskDescriptorFactory,
                    jobCount);

                await executor.ExecuteGraphAsync(
                    workerPool,
                    graph,
                    new JobBuildBehaviour
                    {
                        ForceRemotingForLocalWorker = false,
                    },
                    nullResponseStream,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await worker.StopAsync().ConfigureAwait(false);
                logger.LogInformation("Ending test.");
            }
        }

        [Fact]
        public async Task ExecutionGraphIsReliable()
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var cancellationToken = new CancellationTokenSource(15000).Token;

                    var services = new ServiceCollection();
                    services.AddTasks();
                    services.AddLogging();
                    services.AddGrpcPipes<TcpGrpcPipeFactory>();
                    services.AddOpenGECore();
                    services.AddOpenGEComponentDispatcher();
                    services.AddOpenGEComponentWorker();
                    services.AddOpenGEComponentPreprocessorCache();
                    services.AddProcessExecution();
                    services.AddReservation();
                    services.AddAutoDiscovery();
                    services.AddSingleton<IPreprocessorCacheAccessor, TestPreprocessorCacheAccessor>();
                    // @note: We should really have an "InMemory" cache for unit tests like this, 
                    // but we know this will never actually get used at runtime.
                    services.AddSingleton<IPreprocessorCache>(sp => sp.GetRequiredService<IPreprocessorCacheFactory>().CreateInProcessCache());
                    var provider = services.BuildServiceProvider();

                    var executor = provider.GetRequiredService<IGraphExecutor>();
                    var grpcPipeFactory = provider.GetRequiredService<IGrpcPipeFactory>();
                    var workerPoolFactory = provider.GetRequiredService<ITaskApiWorkerPoolFactory>();
                    var workerFactory = provider.GetRequiredService<IWorkerComponentFactory>();
                    var taskDescriptorFactory = provider.GetRequiredService<LocalTaskDescriptorFactory>();

                    var worker = workerFactory.Create(true);
                    await worker.StartAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var workerClient = grpcPipeFactory.CreateNetworkClient(
                            new IPEndPoint(IPAddress.Loopback, worker.ListeningPort!.Value),
                            x => new TaskApi.TaskApiClient(x));

                        var workerPool = workerPoolFactory.CreateWorkerPool(new TaskApiWorkerPoolConfiguration
                        {
                            EnableNetworkAutoDiscovery = false,
                            LocalWorker = new TaskApiWorkerPoolConfigurationLocalWorker
                            {
                                DisplayName = "Test Client",
                                UniqueId = "1",
                                Client = workerClient,
                            }
                        });

                        var nullResponseStream = new NullGuardedResponseStream<JobResponse>();
                        var graph = CreateGraphForJobCount(
                            taskDescriptorFactory,
                            10);

                        await executor.ExecuteGraphAsync(
                            workerPool,
                            graph,
                            new JobBuildBehaviour
                            {
                                ForceRemotingForLocalWorker = false,
                            },
                            nullResponseStream,
                            cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await worker.StopAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Exception thrown on iteration {i}", ex);
                }
            }
        }
    }
}