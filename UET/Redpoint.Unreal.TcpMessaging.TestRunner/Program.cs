using Redpoint.Unreal.Serialization;
using Redpoint.Unreal.TcpMessaging;
using Redpoint.Unreal.TcpMessaging.MessageTypes;
using System.Net;
using System.Net.Sockets;

var instanceId = Guid.NewGuid();
var sessionId = Guid.NewGuid();
var targetEndpoint = new MessageAddress();


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

// Find workers.
connection.Send(new AutomationWorkerFindWorkers
{
    Changelist = 10000,
    GameName = "ExampleOSS",
    ProcessName = "instance_name",
    SessionId = sessionId,
});
await connection.ReceiveUntilAsync(message =>
{
    switch (message.GetMessageData())
    {
        case AutomationWorkerFindWorkersResponse response:
            sessionId = response.SessionId;
            targetEndpoint = message.SenderAddress.V;
            return Task.FromResult(true);
    }

    return Task.FromResult(false);
}, CancellationToken.None);

// Discover tests.
var discoveredTests = new AutomationWorkerRequestTestsReplyComplete();
connection.Send(targetEndpoint, new AutomationWorkerRequestTests()
{
    DeveloperDirectoryIncluded = true,
    RequestedTestFlags = AutomationTestFlags.EditorContext | AutomationTestFlags.ProductFilter,
});
await connection.ReceiveUntilAsync(message =>
{
    switch (message.GetMessageData())
    {
        case AutomationWorkerRequestTestsReplyComplete response:
            discoveredTests = response;
            return Task.FromResult(true);
    }

    return Task.FromResult(false);
}, CancellationToken.None);

// List discovered tests.
Console.WriteLine($"{discoveredTests.Tests.Count} tests found");
foreach (var test in discoveredTests.Tests.OrderBy(x => x.FullTestPath))
{
    Console.WriteLine(test.FullTestPath);
}

// Run each test in sequence.
foreach (var test in discoveredTests.Tests.OrderBy(x => x.FullTestPath))
{
    Console.Write($"Running {test.FullTestPath} ... ");

    connection.Send(targetEndpoint, new AutomationWorkerRunTests
    {
        ExecutionCount = 1,
        TestName = test.TestName,
        FullTestPath = test.FullTestPath,
        BeautifiedTestName = test.FullTestPath,
        bSendAnalytics = false,
        RoleIndex = 0,
    });
    await connection.ReceiveUntilAsync(message =>
    {
        switch (message.GetMessageData())
        {
            case AutomationWorkerRunTestsReply reply:
                if (reply.TestName == test.TestName)
                {
                    switch (reply.State)
                    {
                        case AutomationState.NotRun:
                            Console.Write(" (not run) ");
                            return Task.FromResult(false);
                        case AutomationState.InProcess:
                            Console.Write(" (in process) ");
                            return Task.FromResult(false);
                        case AutomationState.Skipped:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write($"skip");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine();
                                return Task.FromResult(true);
                            }
                        case AutomationState.Success:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.Write($"pass");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine($" ({reply.Duration} secs)");
                                return Task.FromResult(true);
                            }
                        case AutomationState.Fail:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write($"fail");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine($" ({reply.Duration} secs)");
                                return Task.FromResult(true);
                            }
                        default:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write($"unknown");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine();
                                return Task.FromResult(true);
                            }
                    }
                }
                return Task.FromResult(false);
        }

        return Task.FromResult(false);
    }, CancellationToken.None);
}