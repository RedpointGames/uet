namespace Redpoint.OpenGE.Component.Dispatcher.Tests
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Component.Worker;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Xunit;

    public class GraphExecutionTests
    {
        private class TestPreprocessorCacheAccessor : IPreprocessorCacheAccessor
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

        [Fact]
        public async Task ExecutionGraphBasicTest()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGrpcPipes();
            services.AddOpenGEComponentDispatcher();
            services.AddOpenGEComponentWorker();
            services.AddOpenGEPreprocessorCache();
            services.AddProcessExecution();
            services.AddReservation();
            services.AddSingleton<IPreprocessorCacheAccessor, TestPreprocessorCacheAccessor>();
            // @note: We should really have an "InMemory" cache for unit tests like this, 
            // but we know this will never actually get used at runtime.
            services.AddSingleton<IPreprocessorCache>(sp => sp.GetRequiredService<IPreprocessorCacheFactory>().CreateInProcessCache());
            var provider = services.BuildServiceProvider();

            var grpcPipeFactory = provider.GetRequiredService<IGrpcPipeFactory>();
            var workerFactory = provider.GetRequiredService<IWorkerComponentFactory>();
            var dispatcherFactory = provider.GetRequiredService<IDispatcherComponentFactory>();
            var workerPoolFactory = provider.GetRequiredService<IWorkerPoolFactory>();

            var worker = workerFactory.Create();
            await worker.StartAsync(CancellationToken.None);
            try
            {
                var workerClient = new TaskApi.TaskApiClient(
                    GrpcChannel.ForAddress(worker.ListeningExternalUrl!));
                await using var workerPool = workerPoolFactory.CreateWorkerPool(
                    workerClient);
                var dispatcher = dispatcherFactory.Create(
                    workerPool,
                    null);
                await dispatcher.StartAsync(CancellationToken.None);
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
                    await dispatcher.StopAsync();
                }
            }
            finally
            {
                await worker.StopAsync();
            }
        }
    }
}