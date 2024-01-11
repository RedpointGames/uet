namespace UET.Commands.Internal.TestPdu
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Pdu.Serveredge;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class TestPduCommand
    {
        internal sealed class Options
        {
            public Option<string> Address;
            public Option<string> Community;

            public Options()
            {
                Address = new("--address") { IsRequired = true };
                Community = new("--community", () => "public");
            }
        }

        public static Command CreateTestPduCommand()
        {
            var options = new Options();
            var command = new Command("test-pdu");
            command.AddAllOptions(options);
            command.AddCommonHandler<TestPduCommandInstance>(options);
            return command;
        }

        private sealed class TestPduCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<TestPduCommandInstance> _logger;

            public TestPduCommandInstance(
                Options options,
                ILogger<TestPduCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var address = context.ParseResult.GetValueForOption(_options.Address);
                var community = context.ParseResult.GetValueForOption(_options.Community);

                var factory = new ServeredgePduFactory();
                var pdu = await factory.TryGetAsync(
                    IPAddress.Parse(address!),
                    community!,
                    cancellationToken: context.GetCancellationToken()).ConfigureAwait(false);
                if (pdu == null)
                {
                    _logger.LogError("PDU not found at specified address.");
                    return 1;
                }

                var info = await pdu.GetInformationAsync(context.GetCancellationToken()).ConfigureAwait(false);
                _logger.LogInformation($"Device model: {info.VendorInformation.DeviceModel}");
                _logger.LogInformation($"Authoriative SMI: {info.VendorInformation.AuthoritativeSmi}");
                _logger.LogInformation($"Display name: {info.OwnerInformation.DisplayName}");
                _logger.LogInformation($"Contact person: {info.OwnerInformation.ContactPerson}");
                _logger.LogInformation($"Physical location: {info.OwnerInformation.PhysicalLocation}");
                _logger.LogInformation($"Number of outlets: {info.OutletCount}");

                var state = await pdu.GetStateAsync(context.GetCancellationToken()).ConfigureAwait(false);
                _logger.LogInformation($"Uptime: {state.Uptime}");
                _logger.LogInformation($"Metering (A): {state.Metering.InstantaneousAmperes}");
                _logger.LogInformation($"Metering (W): {state.Metering.InstantaneousWatts}");

                await foreach (var outlet in pdu.GetOutletsAsync())
                {
                    _logger.LogInformation($"Outlet #{outlet.index + 1}: {outlet.state.Name} ({outlet.state.Status})");
                }

                return 0;
            }
        }
    }
}
