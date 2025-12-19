namespace UET.Commands.Internal.Service
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ServiceControl;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class ServiceStopCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<ServiceStopCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("stop");
                })
            .Build();

        internal sealed class Options
        {
            public Argument<string> Name;

            public Options()
            {
                Name = new Argument<string>("service-name");
            }
        }

        private sealed class ServiceStopCommandInstance : ICommandInstance
        {
            private readonly ILogger<ServiceStopCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly Options _options;

            public ServiceStopCommandInstance(
                ILogger<ServiceStopCommandInstance> logger,
                IServiceControl serviceControl,
                Options options)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_options.Name)!;

                _logger.LogInformation($"Stopping service '{name}'...");
                await _serviceControl.StopService(name, context.GetCancellationToken());
                _logger.LogInformation($"Stopped service '{name}'.");

                return 0;
            }
        }
    }
}
