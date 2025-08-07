namespace UET.Commands.Internal.Service
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ServiceControl;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class ServiceStartCommand
    {
        internal sealed class Options
        {
            public Argument<string> Name;

            public Options()
            {
                Name = new Argument<string>("service-name");
            }
        }

        public static Command CreateServiceStartCommand()
        {
            var options = new Options();
            var command = new Command("start");
            command.AddAllOptions(options);
            command.AddCommonHandler<ServiceStartCommandInstance>(options);
            return command;
        }

        private sealed class ServiceStartCommandInstance : ICommandInstance
        {
            private readonly ILogger<ServiceStartCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly Options _options;

            public ServiceStartCommandInstance(
                ILogger<ServiceStartCommandInstance> logger,
                IServiceControl serviceControl,
                Options options)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForArgument(_options.Name)!;

                _logger.LogInformation($"Starting service '{name}'...");
                await _serviceControl.StartService(name, context.GetCancellationToken());
                _logger.LogInformation($"Started service '{name}'.");

                return 0;
            }
        }
    }
}
