using Redpoint.GrpcPipes;
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
    if (string.IsNullOrWhiteSpace(pipeName))
    {
        Console.Error.WriteLine("Expected UET_XGE_SHIM_PIPE_NAME environment variable to be set!");
        context.ExitCode = 1;
        return;
    }

    var client = GrpcPipesCore.CreateClient(
        pipeName,
        channel => new OpenGEAPI.OpenGE.OpenGEClient(channel));

    using (var reader = new StreamReader(new FileStream(context.ParseResult.GetValueForArgument(fileArgument), FileMode.Open, FileAccess.Read, FileShare.Read)))
    {
        var response = client.SubmitJob(new OpenGEAPI.SubmitJobRequest
        {
            BuildNodeName = Environment.GetEnvironmentVariable("UET_XGE_SHIM_BUILD_NODE_NAME") ?? context.ParseResult.GetValueForOption(titleOption) ?? string.Empty,
            JobXml = await reader.ReadToEndAsync(),
        });
        if (await response.ResponseStream.MoveNext(context.GetCancellationToken()))
        {
            context.ExitCode = response.ResponseStream.Current.ExitCode;
        }
        else
        {
            context.ExitCode = 1;
        }
    }
});
return await rootCommand.InvokeAsync(args);