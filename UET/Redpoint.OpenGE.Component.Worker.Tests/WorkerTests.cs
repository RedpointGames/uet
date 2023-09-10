namespace Redpoint.OpenGE.Component.Worker.Tests
{
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.AutoDiscovery;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Tasks;
    using System.Text;
    using Xunit;

    public class WorkerTests
    {
        [SkippableFact]
        public async Task CanExecuteLocalTaskDescriptor()
        {
            Skip.IfNot(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var services = new ServiceCollection();
            services.AddTasks();
            services.AddLogging();
            services.AddOpenGEComponentWorker();
            services.AddProcessExecution();
            services.AddOpenGECore();
            services.AddReservation();
            services.AddAutoDiscovery();
            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<IWorkerComponentFactory>();
            var worker = factory.Create(true);
            await worker.StartAsync(CancellationToken.None);
            try
            {
                var taskClient = new TaskApi.TaskApiClient(
                    GrpcChannel.ForAddress($"http://127.0.0.1:{worker.ListeningPort}"));

                var duplex = taskClient.ReserveCoreAndExecute();

                await duplex.RequestStream.WriteAsync(new ExecutionRequest
                {
                    ReserveCore = new ReserveCoreRequest
                    {
                    },
                });
                Assert.True(
                    await duplex.ResponseStream.MoveNext(CancellationToken.None),
                    "Expected response from worker");
                Assert.Equal(
                    ExecutionResponse.ResponseOneofCase.ReserveCore,
                    duplex.ResponseStream.Current.ResponseCase);

                await duplex.RequestStream.WriteAsync(new ExecutionRequest
                {
                    ExecuteTask = new ExecuteTaskRequest
                    {
                        Descriptor_ = new TaskDescriptor
                        {
                            Local = new LocalTaskDescriptor
                            {
                                Path = @"C:\Windows\system32\cmd.exe",
                                Arguments =
                                {
                                    "/C",
                                    "echo true"
                                },
                                WorkingDirectory = @"C:\Windows\system32",
                            }
                        }
                    }
                });

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                int exitCode = -1;
                while (exitCode == -1 &&
                    await duplex.ResponseStream.MoveNext(CancellationToken.None))
                {
                    Assert.Equal(
                        ExecutionResponse.ResponseOneofCase.ExecuteTask,
                        duplex.ResponseStream.Current.ResponseCase);
                    switch (duplex.ResponseStream.Current.ExecuteTask.Response.DataCase)
                    {
                        case ProcessResponse.DataOneofCase.StandardOutputLine:
                            stdout.AppendLine(duplex.ResponseStream.Current.ExecuteTask.Response.StandardOutputLine);
                            break;
                        case ProcessResponse.DataOneofCase.StandardErrorLine:
                            stdout.AppendLine(duplex.ResponseStream.Current.ExecuteTask.Response.StandardErrorLine);
                            break;
                        case ProcessResponse.DataOneofCase.ExitCode:
                            exitCode = duplex.ResponseStream.Current.ExecuteTask.Response.ExitCode;
                            break;
                    }
                }

                Assert.Equal("true", stdout.ToString().Trim());
                Assert.Equal(string.Empty, stderr.ToString().Trim());
                Assert.Equal(0, exitCode);

                await duplex.RequestStream.CompleteAsync();
            }
            finally
            {
                await worker.StopAsync();
            }
        }
    }
}