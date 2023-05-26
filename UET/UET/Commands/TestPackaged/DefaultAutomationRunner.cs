namespace UET.Commands.TestPackaged
{
    using System.Threading.Tasks;
    using System.Net;
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using Redpoint.Unreal.TcpMessaging;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;

    internal class DefaultAutomationRunner : IAutomationRunner
    {
        private readonly ILogger<DefaultAutomationRunner> _logger;

        public DefaultAutomationRunner(
            ILogger<DefaultAutomationRunner> logger)
        {
            _logger = logger;
        }

        public async Task<int> RunTestsAsync(IPEndPoint endpoint, string testPrefix, string projectName, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var instanceId = Guid.NewGuid();
                var sessionId = Guid.NewGuid();
                var targetEndpoint = new MessageAddress();

                using (var connection = new TcpMessageTransportConnection(endpoint, true))
                {
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
                    }, cancellationToken);

                    // Find workers.
                    connection.Send(new AutomationWorkerFindWorkers
                    {
                        Changelist = 10000,
                        GameName = projectName,
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
                    }, cancellationToken);

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
                    }, cancellationToken);

                    // Order and filter tests.
                    var requestedTests = discoveredTests.Tests
                        .OrderBy(x => x.FullTestPath)
                        .Where(x => x.FullTestPath.StartsWith(testPrefix))
                        .ToList();

                    // List discovered tests.
                    _logger.LogInformation($"{requestedTests.Count} tests found");
                    foreach (var test in requestedTests)
                    {
                        _logger.LogInformation(test.FullTestPath);
                    }

                    // Run each test in sequence.
                    var didFail = false;
                    foreach (var test in requestedTests)
                    {
                        _logger.LogInformation($"Running {test.FullTestPath}...");

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
                                                _logger.LogInformation($"Running {test.FullTestPath}... not run");
                                                return false;
                                            case AutomationState.InProcess:
                                                _logger.LogInformation($"Running {test.FullTestPath}... in process");
                                                return false;
                                        }

                                        foreach (var entry in reply.Entries)
                                        {
                                            if (entry.Event.Type == "Error")
                                            {
                                                _logger.LogError($"  {entry.Event.Message}");
                                            }
                                            else if (entry.Event.Type == "Warning")
                                            {
                                                _logger.LogWarning($"  {entry.Event.Message}");
                                            }
                                            else if (entry.Event.Type == "Info")
                                            {
                                                _logger.LogInformation($"  {entry.Event.Message}");
                                            }
                                            else
                                            {
                                                _logger.LogWarning($"{entry.Event.Type} {entry.Event.Message}");
                                            }
                                        }

                                        switch (reply.State)
                                        {
                                            case AutomationState.Skipped:
                                                _logger.LogInformation($"Running {test.FullTestPath}... \u001b[33mskip\u001b[0m");
                                                return true;
                                            case AutomationState.Success:
                                                _logger.LogInformation($"Running {test.FullTestPath}... \u001b[32mpass\u001b[0m ({reply.Duration:0.00} secs)");
                                                return true;
                                            case AutomationState.Fail:
                                                didFail = true;
                                                _logger.LogInformation($"Running {test.FullTestPath}... \u001b[31mfail\u001b[0m ({reply.Duration:0.00} secs)");
                                                return true;
                                            default:
                                                _logger.LogInformation($"Running {test.FullTestPath}... \u001b[35munknown\u001b[0m");
                                                return true;
                                        }
                                    }
                                    return false;
                            }

                            return false;
                        }, cancellationToken);
                    }

                    return didFail ? 1 : 0;
                }
            });
        }
    }
}
