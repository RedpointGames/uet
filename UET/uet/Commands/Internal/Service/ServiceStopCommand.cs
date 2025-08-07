namespace UET.Commands.Internal.Service
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ServiceControl;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class ServiceStopCommand
    {
        internal sealed class Options
        {
            public Argument<string> Name;

            public Options()
            {
                Name = new Argument<string>("service-name");
            }
        }

        public static Command CreateServiceStopCommand()
        {
            var options = new Options();
            var command = new Command("stop");
            command.AddAllOptions(options);
            command.AddCommonHandler<ServiceStopCommandInstance>(options);
            return command;
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

            public async Task<int> ExecuteAsync(InvocationContext context)
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
