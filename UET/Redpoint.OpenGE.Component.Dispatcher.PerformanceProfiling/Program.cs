using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.AutoDiscovery;
using Redpoint.Collections;
using Redpoint.GrpcPipes;
using Redpoint.OpenGE.Component.Dispatcher;
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
using Redpoint.Logging.SingleLine;
using Fsp;
using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client.Configuration;

var globalServices = new ServiceCollection();
globalServices.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddSingleLineConsoleFormatter(options =>
    {
        options.OmitLogPrefix = false;
        options.TimestampFormat = "HH:mm:ss fff ";
    });
    builder.AddSingleLineConsole();
});
var globalLogger = globalServices.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

for (int i = 1; i <= 100; i++)
{
    globalLogger.LogInformation($"Iteration {i}: Starting up...");

    var startupSt = Stopwatch.StartNew();

    try
    {
        var cancellationToken = new CancellationTokenSource(15000).Token;

        var services = new ServiceCollection();
        services.AddTasks();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSingleLineConsoleFormatter(options =>
            {
                options.OmitLogPrefix = false;
                options.TimestampFormat = "HH:mm:ss fff ";
            });
            builder.AddSingleLineConsole();
        });
        services.AddGrpcPipes();
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
        var workerPoolFactory = provider.GetRequiredService<ITaskApiWorkerPoolFactory>();
        var workerFactory = provider.GetRequiredService<IWorkerComponentFactory>();
        var taskDescriptorFactory = provider.GetRequiredService<LocalTaskDescriptorFactory>();

        var worker = workerFactory.Create(true);
        await worker.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workerClient = new TaskApi.TaskApiClient(
                GrpcChannel.ForAddress(
                    $"http://127.0.0.1:{worker.ListeningPort}",
                    new GrpcChannelOptions
                    {
                        HttpHandler = new SocketsHttpHandler
                        {
                            EnableMultipleHttp2Connections = true,
                            ConnectTimeout = TimeSpan.FromMilliseconds(50),
                        },
                        ServiceConfig = new ServiceConfig
                        {
                            LoadBalancingConfigs =
                            {
                                new PickFirstConfig(),
                            }
                        },
                        Credentials = ChannelCredentials.Insecure,
                    }));

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
                1);

            startupSt.Stop();
            globalLogger.LogInformation($"Iteration {i}: Took {startupSt.ElapsedMilliseconds:0}ms to start up.");

            var executeSt = Stopwatch.StartNew();
            await executor.ExecuteGraphAsync(
                workerPool,
                graph,
                new JobBuildBehaviour
                {
                    ForceRemotingForLocalWorker = false,
                },
                nullResponseStream,
                cancellationToken).ConfigureAwait(false);
            executeSt.Stop();
            globalLogger.LogInformation($"Iteration {i}: Took {executeSt.ElapsedMilliseconds:0}ms to execute graph.");
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

Graph CreateGraphForJobCount(
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
                Arguments = Array.Empty<string>(),
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

sealed class TestPreprocessorCacheAccessor : IPreprocessorCacheAccessor
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

sealed class NullGuardedResponseStream<T> : IGuardedResponseStream<T>
{
    public Task WriteAsync(T response, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}