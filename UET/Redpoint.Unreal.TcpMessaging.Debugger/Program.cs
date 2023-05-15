using Redpoint.Unreal.TcpMessaging;
using Redpoint.Unreal.TcpMessaging.MessageTypes;
using System.Diagnostics;
using System.Net;

var instanceId = Guid.NewGuid();
var sessionId = Guid.NewGuid();

var connection = new TcpMessageTransportConnection(new IPEndPoint(IPAddress.Loopback, 6666));

// Detect the remote's engine version so we can pretend to be the same.
var engineVersion = 0;
var buildDate = string.Empty;
var sessionOwner = string.Empty;
bool gotEngineVersion = false, gotSessionId = false;
connection.Send(new EngineServicePing());
connection.Send(new SessionServicePing { UserName = string.Empty });
connection.ReceiveUntil(message =>
{
    switch (message.GetMessageData())
    {
        case EngineServicePong pong:
            engineVersion = pong.EngineVersion;
            gotEngineVersion = true;
            return gotEngineVersion && gotSessionId;
        case SessionServicePong pong:
            sessionId = pong.SessionId;
            buildDate = pong.BuildDate;
            sessionOwner = pong.SessionOwner;
            gotSessionId = true;
            return gotEngineVersion && gotSessionId;
    }

    return false;
});

// Ask the remote to send message logs.
connection.Send(new SessionServiceLogSubscribe());

// Periodically ping the remote.
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(5000);

        connection.Send(new EngineServicePing());
        connection.Send(new SessionServicePing { UserName = sessionOwner });
    }
});

// Now observe the messages and pretend to be an Unreal Engine instance.
connection.ReceiveUntil(message =>
{
    switch (message.GetMessageData())
    {
        case EngineServicePing ping:
            connection.Respond(message, new EngineServicePong
            {
                EngineVersion = engineVersion,
                InstanceId = instanceId,
                SessionId = sessionId,
                InstanceType = "Editor",
                CurrentLevel = string.Empty,
                HasBegunPlay = false,
                WorldTimeSeconds = 0.0f,
            });
            break;
        case SessionServicePing ping:
            connection.Respond(message, new SessionServicePong
            {
                Authorized = true,
                BuildDate = buildDate,
                DeviceName = "DEBUG-CONTROL",
                InstanceId = instanceId,
                InstanceName = $"DEBUG-CONTROL-{Process.GetCurrentProcess().Id}",
                PlatformName = "WindowsEditor",
                SessionId = sessionId,
                SessionName = string.Empty,
                SessionOwner = sessionOwner,
                Standalone = false,
            });
            break;
        case AutomationWorkerFindWorkers findWorkers:
            connection.Respond(message, new AutomationWorkerFindWorkersResponse
            {
                DeviceName = "DEBUG-CONTROL",
                InstanceName = $"DEBUG-CONTROL-{Process.GetCurrentProcess().Id}",
                Platform = "Windows",
                OSVersionName = "Windows 11",
                ModelName = string.Empty,
                GPUName = string.Empty,
                CPUModelName = string.Empty,
                RAMInGB = 32,
                RenderModeName = string.Empty,
                SessionId = sessionId,
                RHIName = string.Empty,
            });
            break;
        case AutomationWorkerRequestTests requestTests:
            connection.Respond(message, new AutomationWorkerRequestTestsReplyComplete
            {
                Tests = new List<AutomationWorkerSingleTestReply>(),
            });
            break;
        case SessionServiceLogSubscribe logSubscribe:
            // Just send a single log message so we know the debugger is connected.
            connection.Respond(message, new SessionServiceLog
            {
                InstanceId = instanceId,
                Category = "LogDebugger",
                Data = "The Redpoint TCP Messaging debugger has connected."
            });
            break;
        default:
            connection.WriteConsole(() =>
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"(message type {message.AssetPath} was unhandled by the main loop!)");
                Console.ForegroundColor = oldColor;
            });
            break;
    }

    return false;
});
