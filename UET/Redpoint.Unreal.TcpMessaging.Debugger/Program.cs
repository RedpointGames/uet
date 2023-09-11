using Redpoint.Unreal.TcpMessaging;
using Redpoint.Unreal.TcpMessaging.MessageTypes;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var instanceId = Guid.NewGuid();
var sessionId = Guid.NewGuid();

var connection = await TcpMessageTransportConnection.CreateAsync(async () =>
{
    var client = new TcpClient();
    await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 6666));
    return client;
});

// Detect the remote's engine version so we can pretend to be the same.
var engineVersion = 0;
var buildDate = string.Empty;
var sessionOwner = string.Empty;
bool gotEngineVersion = false, gotSessionId = false;
connection.Send(new EngineServicePing());
connection.Send(new SessionServicePing { UserName = string.Empty });
await connection.ReceiveUntilAsync(message =>
{
    switch (message.GetMessageData())
    {
        case EngineServicePong pong:
            engineVersion = pong.EngineVersion;
            gotEngineVersion = true;
            return Task.FromResult(gotEngineVersion && gotSessionId);
        case SessionServicePong pong:
            sessionId = pong.SessionId;
            buildDate = pong.BuildDate;
            sessionOwner = pong.SessionOwner;
            gotSessionId = true;
            return Task.FromResult(gotEngineVersion && gotSessionId);
    }

    return Task.FromResult(false);
}, CancellationToken.None);

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
await connection.ReceiveUntilAsync(message =>
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
                InstanceName = $"DEBUG-CONTROL-{Environment.ProcessId}",
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
                InstanceName = $"DEBUG-CONTROL-{Environment.ProcessId}",
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
            Console.WriteLine($"(message type {message.AssetPath} was unhandled by the main loop!)");
            break;
    }

    return Task.FromResult(false);
}, CancellationToken.None);
