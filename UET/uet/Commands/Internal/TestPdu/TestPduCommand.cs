namespace UET.Commands.Internal.TestPdu
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Pdu.Abstractions;
    using Redpoint.Pdu.CyberPower;
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
            public Option<bool> Reset;

            public Options()
            {
                Address = new("--address") { IsRequired = true };
                Community = new("--community", () => "public");
                Reset = new("--reset", description: "If set, reset the meters on the PDU.");
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
                var reset = context.ParseResult.GetValueForOption(_options.Reset);

                IPdu? pdu = null;
                foreach (var factory in new IPduFactory[] { new ServeredgePduFactory(), new CyberPowerPduFactory() })
                {
                    pdu = await factory.TryGetAsync(
                        IPAddress.Parse(address!),
                        community!,
                        cancellationToken: context.GetCancellationToken()).ConfigureAwait(false);
                    if (pdu != null)
                    {
                        break;
                    }
                }
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
                if (state.Metering.Type == PduMeteringType.Instantaneous)
                {
                    _logger.LogInformation($"Metering (A): {state.Metering.InstantaneousAmperes}");
                    _logger.LogInformation($"Metering (W): {state.Metering.InstantaneousWatts}");
                }
                else if (state.Metering.Type == PduMeteringType.Accumulated)
                {
                    _logger.LogInformation($"Metering (Start): {state.Metering.AccumulatedStartTime}");
                    _logger.LogInformation($"Metering (Hours): {state.Metering.AccumulatedDuration.TotalHours}");
                    _logger.LogInformation($"Metering (kWh): {state.Metering.AccumulatedKilowattHours}");
                    if (reset)
                    {
                        await pdu.ResetAccumulatedMeteringAsync(context.GetCancellationToken()).ConfigureAwait(false);
                    }
                }

                await foreach (var outlet in pdu.GetOutletsAsync())
                {
                    _logger.LogInformation($"Outlet #{outlet.index + 1}: {outlet.state.Name}");
                    _logger.LogInformation($"  Status: {outlet.state.Status}");
                    if (outlet.state.Metering.Type == PduMeteringType.Instantaneous)
                    {
                        _logger.LogInformation($"  Metering (A): {outlet.state.Metering.InstantaneousAmperes}");
                        _logger.LogInformation($"  Metering (W): {outlet.state.Metering.InstantaneousWatts}");
                    }
                    else if (state.Metering.Type == PduMeteringType.Accumulated)
                    {
                        _logger.LogInformation($"  Metering (Start): {outlet.state.Metering.AccumulatedStartTime}");
                        _logger.LogInformation($"  Metering (Hours): {outlet.state.Metering.AccumulatedDuration.TotalHours}");
                        _logger.LogInformation($"  Metering (kWh): {outlet.state.Metering.AccumulatedKilowattHours}");
                        if (reset)
                        {
                            await pdu.ResetOutletAccumulatedMeteringAsync(outlet.index, context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                }

                return 0;
            }
        }
    }
}
