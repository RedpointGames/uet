using Grpc.Core;
using Redpoint.GrpcPipes;
using Redpoint.OpenGE.Protocol;
using System.CommandLine;
using System.CommandLine.Invocation;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var rebuildOption = new Option<bool>("/Rebuild");
var noLogoOption = new Option<bool>("/NoLogo");
var showAgentOption = new Option<bool>("/ShowAgent");
var showTimeOption = new Option<bool>("/ShowTime");
var titleOption = new Option<string>("/Title");
var noWaitOption = new Option<bool>("/NoWait");
var useIdeMonitor = new Option<bool>("/UseIdeMonitor");
var fileArgument = new Argument<string>();

var rootCommand = new Command("openge-shim", "OpenGE shim to emulate xgConsole.");
rootCommand.AddOption(rebuildOption);
rootCommand.AddOption(noLogoOption);
rootCommand.AddOption(showAgentOption);
rootCommand.AddOption(showTimeOption);
rootCommand.AddOption(titleOption);
rootCommand.AddOption(noWaitOption);
rootCommand.AddOption(useIdeMonitor);
rootCommand.AddArgument(fileArgument);
rootCommand.SetHandler(async (InvocationContext context) =>
{
    var pipeName = Environment.GetEnvironmentVariable("UET_XGE_SHIM_PIPE_NAME");
    var pipeNamespace = GrpcPipeNamespace.User;
    if (string.IsNullOrWhiteSpace(pipeName))
    {
        pipeName = "OpenGE";
        pipeNamespace = GrpcPipeNamespace.Computer;
    }

    var client = GrpcPipesCore.CreateClient(
        pipeName,
        pipeNamespace,
        channel => new JobApi.JobApiClient(channel));

    using (var reader = new StreamReader(new FileStream(context.ParseResult.GetValueForArgument(fileArgument), FileMode.Open, FileAccess.Read, FileShare.Read)))
    {
        var request = new SubmitJobRequest
        {
            BuildNodeName = Environment.GetEnvironmentVariable("UET_XGE_SHIM_BUILD_NODE_NAME") ?? context.ParseResult.GetValueForOption(titleOption) ?? string.Empty,
            JobXml = await reader.ReadToEndAsync(),
            WorkingDirectory = Environment.CurrentDirectory,
        };
        var originalEnvs = Environment.GetEnvironmentVariables();
        foreach (var key in originalEnvs.Keys.OfType<string>())
        {
            var v = originalEnvs[key] as string;
            if (v != null)
            {
                request.EnvironmentVariables[key] = v;
            }
        }
        var response = client.SubmitJob(request);

        // In case we don't get JobComplete.
        context.ExitCode = 1;

        int tasksTotal = 0;
        int tasksInFlight = 0;
        int tasksComplete = 0;
        try
        {
            while (await response.ResponseStream.MoveNext(context.GetCancellationToken()))
            {
                switch (response.ResponseStream.Current.ResponseCase)
                {
                    case JobResponse.ResponseOneofCase.JobParsed:
                        Console.WriteLine($"{response.ResponseStream.Current.JobParsed.TotalTasks} tasks to execute on OpenGE");
                        tasksTotal = response.ResponseStream.Current.JobParsed.TotalTasks;
                        break;
                    case JobResponse.ResponseOneofCase.TaskStarted:
                        tasksInFlight++;
                        var taskStarted = response.ResponseStream.Current.TaskStarted;
                        Console.WriteLine($"[{tasksComplete}/{tasksTotal}] {taskStarted.DisplayName} [started on core {taskStarted.WorkerCoreNumber} on {taskStarted.WorkerMachineName}]");
                        break;
                    case JobResponse.ResponseOneofCase.TaskOutput:
                        var taskOutput = response.ResponseStream.Current.TaskOutput;
                        switch (taskOutput.OutputCase)
                        {
                            case TaskOutputResponse.OutputOneofCase.StandardOutputLine:
                                Console.WriteLine(taskOutput.StandardOutputLine);
                                break;
                            case TaskOutputResponse.OutputOneofCase.StandardErrorLine:
                                Console.Error.WriteLine(taskOutput.StandardErrorLine);
                                break;
                        }
                        break;
                    case JobResponse.ResponseOneofCase.TaskCompleted:
                        tasksComplete++;
                        tasksInFlight--;
                        var taskCompleted = response.ResponseStream.Current.TaskCompleted;
                        switch (taskCompleted.Status)
                        {
                            case TaskCompletionStatus.TaskCompletionSuccess:
                                Console.WriteLine($"[{tasksComplete}/{tasksTotal}] {taskCompleted.DisplayName} [success in {taskCompleted.TotalSeconds:F2} secs]");
                                break;
                            case TaskCompletionStatus.TaskCompletionException:
                                Console.WriteLine($"[{tasksComplete}/{tasksTotal}] {taskCompleted.DisplayName} [exception in {taskCompleted.TotalSeconds:F2} secs]");
                                Console.WriteLine("Exception propagated from OpenGE executor: " + taskCompleted.ExceptionMessage);
                                break;
                            case TaskCompletionStatus.TaskCompletionFailure:
                                Console.WriteLine($"[{tasksComplete}/{tasksTotal}] {taskCompleted.DisplayName} [failed in {taskCompleted.TotalSeconds:F2} secs; exit code {taskCompleted.ExitCode}]");
                                break;
                            case TaskCompletionStatus.TaskCompletionCancelled:
                                Console.WriteLine($"[{tasksComplete}/{tasksTotal}] {taskCompleted.DisplayName} [cancelled in {taskCompleted.TotalSeconds:F2} secs]");
                                break;
                        }
                        break;
                    case JobResponse.ResponseOneofCase.JobComplete:
                        var jobComplete = response.ResponseStream.Current.JobComplete;
                        switch (jobComplete.Status)
                        {
                            case JobCompletionStatus.JobCompletionSuccess:
                                Console.WriteLine($"OpenGE job completed successfully.");
                                context.ExitCode = 0;
                                break;
                            case JobCompletionStatus.JobCompletionFailure:
                                Console.WriteLine($"OpenGE job failed, see above for errors.");
                                context.ExitCode = 1;
                                break;
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
        {
            context.ExitCode = 1;
        }
        catch (RpcException) when (context.GetCancellationToken().IsCancellationRequested)
        {
            context.ExitCode = 1;
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"OpenGE executor failed with exception: {ex}");
            context.ExitCode = 1;
        }
    }
});
return await rootCommand.InvokeAsync(args);