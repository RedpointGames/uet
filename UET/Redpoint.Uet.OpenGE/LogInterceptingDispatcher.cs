namespace Redpoint.Uet.OpenGE
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;
    using static Crayon.Output;

    internal class LogInterceptingDispatcher : JobApi.JobApiBase
    {
        private readonly ILogger _logger;
        private readonly JobApi.JobApiClient _upstream;

        public LogInterceptingDispatcher(
            ILogger logger,
            JobApi.JobApiClient upstream)
        {
            _logger = logger;
            _upstream = upstream;
        }

        public override async Task<PingJobServiceResponse> PingJobService(PingJobServiceRequest request, ServerCallContext context)
        {
            return await _upstream.PingJobServiceAsync(request, cancellationToken: context.CancellationToken);
        }

        public override async Task SubmitJob(SubmitJobRequest request, IServerStreamWriter<JobResponse> responseStream, ServerCallContext context)
        {
            int tasksTotal = 0;
            int tasksComplete = 0;
            string GetBuildStatusLogPrefix()
            {
                var remainingTasks = tasksTotal - tasksComplete;
                var percent = (1.0 - (tasksTotal == 0 ? 0.0 : ((double)remainingTasks / tasksTotal))) * 100.0;
                var tasksTotalLength = tasksTotal.ToString().Length;
                var line = $"[{request.BuildNodeName}] [{percent,3:0}%, {tasksComplete.ToString().PadLeft(tasksTotalLength)}/{tasksTotal}]";
                return line;
            }

            var upstream = _upstream.SubmitJob(request, cancellationToken: context.CancellationToken);
            while (await upstream.ResponseStream.MoveNext(context.CancellationToken))
            {
                var response = upstream.ResponseStream.Current;
                switch (response.ResponseCase)
                {
                    case JobResponse.ResponseOneofCase.JobParsed:
                        tasksTotal = response.JobParsed.TotalTasks;
                        await responseStream.WriteAsync(response);
                        break;
                    case JobResponse.ResponseOneofCase.JobComplete:
                        await responseStream.WriteAsync(response);
                        break;
                    case JobResponse.ResponseOneofCase.TaskPreparing:
                        _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskPreparing.DisplayName} {Bright.Black($"[{response.TaskPreparing.OperationDescription}]")}");
                        break;
                    case JobResponse.ResponseOneofCase.TaskPrepared:
                        _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskPrepared.DisplayName} {Bright.Black($"[{response.TaskPrepared.OperationCompletedDescription} in {response.TaskPrepared.TotalSeconds:F2} secs]")}");
                        break;
                    case JobResponse.ResponseOneofCase.TaskOutput:
                        switch (response.TaskOutput.OutputCase)
                        {
                            case TaskOutputResponse.OutputOneofCase.StandardOutputLine:
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskOutput.StandardOutputLine}");
                                break;
                            case TaskOutputResponse.OutputOneofCase.StandardErrorLine:
                                if (OperatingSystem.IsWindows())
                                {
                                    _logger.LogError($"{GetBuildStatusLogPrefix()} {response.TaskOutput.StandardErrorLine}");
                                }
                                else
                                {
                                    // @note: On macOS, some output to standard error is just normal
                                    // output and doesn't represent an error state.
                                    _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskOutput.StandardErrorLine}");
                                }
                                break;
                        }
                        break;
                    case JobResponse.ResponseOneofCase.TaskStarted:
                        _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskStarted.DisplayName} {Bright.Black($"[started on core {response.TaskStarted.WorkerCoreNumber} on {response.TaskStarted.WorkerMachineName}]")}");
                        break;
                    case JobResponse.ResponseOneofCase.TaskCompleted:
                        tasksComplete++;
                        var taskCompleted = response.TaskCompleted;
                        switch (taskCompleted.Status)
                        {
                            case TaskCompletionStatus.TaskCompletionSuccess:
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskCompleted.DisplayName} {Bright.Green($"[success in {taskCompleted.TotalSeconds:F2} secs]")}");
                                break;
                            case TaskCompletionStatus.TaskCompletionException:
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskCompleted.DisplayName} {Bright.Red($"[exception in {taskCompleted.TotalSeconds:F2} secs]")}");
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskCompleted.DisplayName} Exception propagated from OpenGE executor: " + taskCompleted.ExceptionMessage);
                                break;
                            case TaskCompletionStatus.TaskCompletionFailure:
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskCompleted.DisplayName} {taskCompleted.DisplayName} {Bright.Red($"[failed in {taskCompleted.TotalSeconds:F2} secs; exit code {taskCompleted.ExitCode}]")}");
                                break;
                            case TaskCompletionStatus.TaskCompletionCancelled:
                                _logger.LogInformation($"{GetBuildStatusLogPrefix()} {response.TaskCompleted.DisplayName} {taskCompleted.DisplayName} {Bright.Yellow($"[cancelled in {taskCompleted.TotalSeconds:F2} secs]")}");
                                break;
                        }
                        break;
                }
            }
        }
    }
}
