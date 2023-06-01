// @note: In future, this shim needs to submit the jobs to the parent UET process
// via a gRPC named socket. This will ensure that core reservation is correct
// when jobs are running in parallel, and would allow us to actually distribute
// work across multiple machines in future.
//
// For now though, doing the execution inside the shim is good enough to get this
// over the line.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.OpenGE.Executor;
using Redpoint.ProcessExecution;
using Redpoint.UET.Core;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

// Ensure we do not re-use MSBuild processes, because our dotnet executables
// will often be inside UEFS packages and mounts that might go away at any time.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

var rebuildOption = new Option<bool>("/Rebuild");
var noLogoOption = new Option<bool>("/NoLogo");
var showAgentOption = new Option<bool>("/ShowAgent");
var showTimeOption = new Option<bool>("/ShowTime");
var titleOption = new Option<string>("/Title");
var fileArgument = new Argument<string>();

var rootCommand = new Command("openge-shim", "OpenGE shim to emulate xgConsole.");
rootCommand.AddOption(rebuildOption);
rootCommand.AddOption(noLogoOption);
rootCommand.AddOption(showAgentOption);
rootCommand.AddOption(showTimeOption);
rootCommand.AddOption(titleOption);
rootCommand.AddArgument(fileArgument);
rootCommand.SetHandler(async (InvocationContext context) =>
{
    var services = new ServiceCollection();
    services.AddUETCore(omitLogPrefix: true);
    services.AddProcessExecution();
    services.AddOpenGEExecutor();
    var sp = services.BuildServiceProvider();

    var cts = CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());
    var st = Stopwatch.StartNew();
    var logger = sp.GetRequiredService<ILogger<Command>>();

    try
    {
        using (var stream = new FileStream(context.ParseResult.GetValueForArgument(fileArgument), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var executorFactory = sp.GetRequiredService<IOpenGEExecutorFactory>();
            var executor = executorFactory.CreateExecutor(stream, turnOffExtraLogInfo: Environment.GetEnvironmentVariable("UET_FORCE_XGE_SHIM") == "1");
            context.ExitCode = await executor.ExecuteAsync(cts);
            context.GetCancellationToken().ThrowIfCancellationRequested();
            if (context.ExitCode == 0)
            {
                logger.LogInformation($"\u001b[32msuccess\u001b[0m in {st.Elapsed.TotalSeconds:F2} sec");
            }
            else
            {
                logger.LogInformation($"\u001b[31mfailure\u001b[0m in {st.Elapsed.TotalSeconds:F2} sec");
            }
        }
    }
    catch (TaskCanceledException)
    {
        logger.LogInformation($"\u001b[33mcancelled\u001b[0m in {st.Elapsed.TotalSeconds:F2} sec");
        context.ExitCode = 1;
    }
});
return await rootCommand.InvokeAsync(args);