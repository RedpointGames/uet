using Grpc.Core;
using Redpoint.GrpcPipes;
using Redpoint.OpenGE.Protocol;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;

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
var silentOption = new Option<bool>("/Silent");
var commandOption = new Option<string>("/Command");
var fileArgument = new Argument<string>();

var rootCommand = new Command("openge-shim", "OpenGE shim to emulate xgConsole.");
rootCommand.AddOption(rebuildOption);
rootCommand.AddOption(noLogoOption);
rootCommand.AddOption(showAgentOption);
rootCommand.AddOption(showTimeOption);
rootCommand.AddOption(titleOption);
rootCommand.AddOption(noWaitOption);
rootCommand.AddOption(useIdeMonitor);
rootCommand.AddOption(silentOption);
rootCommand.AddOption(commandOption);
rootCommand.AddArgument(fileArgument);
rootCommand.SetHandler(async (InvocationContext context) =>
{
    if (context.ParseResult.GetValueForOption(commandOption) == "Unused")
    {
        // This is UBT testing Incredibuild to see if another build is running, but OpenGE
        // permits multiple builds in parallel.
        context.ExitCode = 0;
        return;
    }

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
        var behaviour = new JobBuildBehaviour();
        if (Environment.GetEnvironmentVariable("OPENGE_FORCE_REMOTING_FOR_LOCAL_WORKER") == "1")
        {
            behaviour.ForceRemotingForLocalWorker = true;
        }
        var request = new SubmitJobRequest
        {
            BuildNodeName = Environment.GetEnvironmentVariable("UET_XGE_SHIM_BUILD_NODE_NAME") ?? context.ParseResult.GetValueForOption(titleOption) ?? string.Empty,
            JobXml = await reader.ReadToEndAsync().ConfigureAwait(false),
            WorkingDirectory = Environment.CurrentDirectory,
            BuildBehaviour = behaviour,
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
        void WriteLine(string message)
        {
            var remainingTasks = tasksTotal - tasksComplete;
            var percent = (1.0 - (tasksTotal == 0 ? 0.0 : ((double)remainingTasks / tasksTotal))) * 100.0;
            var tasksTotalLength = tasksTotal.ToString(CultureInfo.InvariantCulture).Length;
            var line = $"[{percent,3:0}%, {tasksInFlight.ToString(CultureInfo.InvariantCulture).PadLeft(tasksTotalLength)}->{tasksComplete.ToString(CultureInfo.InvariantCulture).PadLeft(tasksTotalLength)}/{tasksTotal}] {message}";
            Console.WriteLine(line);
        }
        try
        {
            while (await response.ResponseStream.MoveNext(context.GetCancellationToken()).ConfigureAwait(false))
            {
                switch (response.ResponseStream.Current.ResponseCase)
                {
                    case JobResponse.ResponseOneofCase.JobParsed:
                        Console.WriteLine($"{response.ResponseStream.Current.JobParsed.TotalTasks} tasks to execute on OpenGE");
                        tasksTotal = response.ResponseStream.Current.JobParsed.TotalTasks;
                        break;
                    case JobResponse.ResponseOneofCase.TaskPreparing:
                        var taskPreparing = response.ResponseStream.Current.TaskPreparing;
                        WriteLine($"{taskPreparing.DisplayName} [{taskPreparing.OperationDescription}]");
                        break;
                    case JobResponse.ResponseOneofCase.TaskPrepared:
                        var taskPrepared = response.ResponseStream.Current.TaskPrepared;
                        WriteLine($"{taskPrepared.DisplayName} [{taskPrepared.OperationCompletedDescription} in {taskPrepared.TotalSeconds:F2} secs]");
                        break;
                    case JobResponse.ResponseOneofCase.TaskStarted:
                        tasksInFlight++;
                        var taskStarted = response.ResponseStream.Current.TaskStarted;
                        WriteLine($"{taskStarted.DisplayName} [started on core {taskStarted.WorkerCoreNumber} on {taskStarted.WorkerMachineName}]");
                        break;
                    case JobResponse.ResponseOneofCase.TaskOutput:
                        var taskOutput = response.ResponseStream.Current.TaskOutput;
                        switch (taskOutput.OutputCase)
                        {
                            case TaskOutputResponse.OutputOneofCase.StandardOutputLine:
                                WriteLine(taskOutput.StandardOutputLine);
                                break;
                            case TaskOutputResponse.OutputOneofCase.StandardErrorLine:
                                WriteLine(taskOutput.StandardErrorLine);
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
                                WriteLine($"{taskCompleted.DisplayName} [success in {taskCompleted.TotalSeconds:F2} secs]");
                                break;
                            case TaskCompletionStatus.TaskCompletionException:
                                WriteLine($"{taskCompleted.DisplayName} [exception in {taskCompleted.TotalSeconds:F2} secs]");
                                WriteLine("Exception propagated from OpenGE executor: " + taskCompleted.ExceptionMessage);
                                break;
                            case TaskCompletionStatus.TaskCompletionFailure:
                                WriteLine($"{taskCompleted.DisplayName} [failed in {taskCompleted.TotalSeconds:F2} secs; exit code {taskCompleted.ExitCode}]");
                                break;
                            case TaskCompletionStatus.TaskCompletionCancelled:
                                WriteLine($"{taskCompleted.DisplayName} [cancelled in {taskCompleted.TotalSeconds:F2} secs]");
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
                                if (!string.IsNullOrWhiteSpace(jobComplete.ExceptionMessage))
                                {
                                    Console.WriteLine(jobComplete.ExceptionMessage);
                                }
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
return await rootCommand.InvokeAsync(args).ConfigureAwait(false);