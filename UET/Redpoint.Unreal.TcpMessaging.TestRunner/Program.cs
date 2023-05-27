using Redpoint.Unreal.Serialization;
using Redpoint.Unreal.TcpMessaging;
using Redpoint.Unreal.TcpMessaging.MessageTypes;
using System.Net;

var instanceId = Guid.NewGuid();
var sessionId = Guid.NewGuid();
var targetEndpoint = new MessageAddress();

var connection = new TcpMessageTransportConnection(new IPEndPoint(IPAddress.Loopback, 6666), true);

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
}, CancellationToken.None);

// Find workers.
connection.Send(new AutomationWorkerFindWorkers
{
    Changelist = 10000,
    GameName = "ExampleOSS",
    ProcessName = "instance_name",
    SessionId = sessionId,
});
connection.ReceiveUntil(message =>
{
    switch (message.GetMessageData())
    {
        case AutomationWorkerFindWorkersResponse response:
            sessionId = response.SessionId;
            targetEndpoint = message.SenderAddress;
            return true;
    }

    return false;
}, CancellationToken.None);

// Discover tests.
var discoveredTests = new AutomationWorkerRequestTestsReplyComplete();
connection.Send(targetEndpoint, new AutomationWorkerRequestTests()
{
    DeveloperDirectoryIncluded = true,
    RequestedTestFlags = AutomationTestFlags.EditorContext | AutomationTestFlags.ProductFilter,
});
connection.ReceiveUntil(message =>
{
    switch (message.GetMessageData())
    {
        case AutomationWorkerRequestTestsReplyComplete response:
            discoveredTests = response;
            return true;
    }

    return false;
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
    connection.ReceiveUntil(message =>
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
                            return false;
                        case AutomationState.InProcess:
                            Console.Write(" (in process) ");
                            return false;
                        case AutomationState.Skipped:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write($"skip");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine();
                                return true;
                            }
                        case AutomationState.Success:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.Write($"pass");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine($" ({reply.Duration} secs)");
                                return true;
                            }
                        case AutomationState.Fail:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write($"fail");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine($" ({reply.Duration} secs)");
                                return true;
                            }
                        default:
                            {
                                var oldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write($"unknown");
                                Console.ForegroundColor = oldColor;
                                Console.WriteLine();
                                return true;
                            }
                    }
                }
                return false;
        }

        return false;
    }, CancellationToken.None);
}