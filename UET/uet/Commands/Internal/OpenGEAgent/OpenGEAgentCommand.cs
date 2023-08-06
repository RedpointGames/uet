namespace UET.Commands.Internal.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE;
    using Redpoint.OpenGE.Agent;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.OpenGE;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class OpenGEAgentCommand
    {
        internal class Options
        {
        }

        public static Command CreateOpenGEAgentCommand()
        {
            var options = new Options();
            var command = new Command("openge-agent");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEAgentCommandInstance>(options);
            return command;
        }

        private class OpenGEAgentCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEAgentCommandInstance> _logger;
            private readonly IOpenGEAgentFactory _openGEAgentFactory;
            private readonly Options _options;

            public OpenGEAgentCommandInstance(
                ILogger<OpenGEAgentCommandInstance> logger,
                IOpenGEAgentFactory openGEAgentFactory,
                Options options)
            {
                _logger = logger;
                _openGEAgentFactory = openGEAgentFactory;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var agent = _openGEAgentFactory.CreateAgent(true);
                await agent.StartAsync();
                try
                {
                    _logger.LogInformation("The OpenGE system-wide agent is now running.");
                    while (!context.GetCancellationToken().IsCancellationRequested)
                    {
                        await Task.Delay(10000, context.GetCancellationToken());
                    }
                }
                catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                {
                }
                finally
                {
                    await agent.StopAsync();
                    _logger.LogInformation("The OpenGE system-wide agent has stopped.");
                }
                return 0;
            }
        }
    }
}
