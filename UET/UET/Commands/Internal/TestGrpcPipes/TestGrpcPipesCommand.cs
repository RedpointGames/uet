namespace UET.Commands.Internal.TestGrpcPipes
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using TestPipes;
    using static TestPipes.TestService;

    internal class TestGrpcPipesCommand
    {
        internal class Options
        {
        }

        public static Command CreateTestGrpcPipesCommand()
        {
            var options = new Options();
            var command = new Command("test-grpc-pipes");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestGrpcPipesCommandInstance>(options);
            return command;
        }

        private class TestGrpcPipesCommandInstance : TestServiceBase, ICommandInstance
        {
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly ILogger<TestGrpcPipesCommandInstance> _logger;
            private bool _methodReceived = false;

            public TestGrpcPipesCommandInstance(
                IGrpcPipeFactory grpcPipeFactory,
                ILogger<TestGrpcPipesCommandInstance> logger)
            {
                _grpcPipeFactory = grpcPipeFactory;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var pipeName = $"test-grpc-pipes-{Process.GetCurrentProcess().Id}";

                var server = _grpcPipeFactory.CreateServer(
                    pipeName,
                    GrpcPipeNamespace.User,
                    this);
                await server.StartAsync();

                var client = _grpcPipeFactory.CreateClient(
                    pipeName,
                    GrpcPipeNamespace.User,
                    channel => new TestServiceClient(channel));

                await client.TestMethodAsync(new TestRequest());

                await server.StopAsync();

                if (_methodReceived)
                {
                    _logger.LogInformation("gRPC pipe test complete.");
                    return 0;
                }
                else
                {
                    _logger.LogError("gRPC pipe test failed.");
                    return 1;
                }
            }

            public override Task<TestResponse> TestMethod(TestRequest request, ServerCallContext context)
            {
                _methodReceived = true;
                return Task.FromResult(new TestResponse());
            }
        }
    }
}
