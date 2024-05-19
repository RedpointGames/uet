namespace UET.Commands.Internal.WakeOnLan
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net;
    using System.Net.Sockets;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed partial class WakeOnLanCommand
    {
        internal sealed class Options
        {
            public Option<string> MacAddress;
            public Option<string> IpAddress;

            public Options()
            {
                MacAddress = new Option<string>("--mac-address") { IsRequired = true };
                IpAddress = new Option<string>("--ip-address");

                MacAddress.Description = "A MAC address in the form of 00:00:00:00:00:00.";
                IpAddress.Description = "An IPv4 or IPv6 address.";

                MacAddress.AddAlias("-m");
                MacAddress.AddAlias("--mac");
                IpAddress.AddAlias("-i");
                IpAddress.AddAlias("--ip");
            }
        }

        public static Command CreateWakeOnLanCommand()
        {
            var options = new Options();
            var command = new Command("wake-on-lan");
            command.AddAlias("wol");
            command.AddAllOptions(options);
            command.AddCommonHandler<WakeOnLanCommandInstance>(options);
            return command;
        }

        private sealed partial class WakeOnLanCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<WakeOnLanCommandInstance> _logger;

            public WakeOnLanCommandInstance(
                Options options,
                ILogger<WakeOnLanCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            [GeneratedRegex("^([0-9a-fA-F]{2})\\:([0-9a-fA-F]{2})\\:([0-9a-fA-F]{2})\\:([0-9a-fA-F]{2})\\:([0-9a-fA-F]{2})\\:([0-9a-fA-F]{2})$", RegexOptions.None, "en-US")]
            private static partial Regex MacAddressRegex();

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var macAddress = context.ParseResult.GetValueForOption(_options.MacAddress);
                var ipAddress = context.ParseResult.GetValueForOption(_options.IpAddress);

                if (string.IsNullOrWhiteSpace(macAddress))
                {
                    return 1;
                }

                macAddress = macAddress.ToUpperInvariant();
                var parsedMacAddress = MacAddressRegex().Match(macAddress); 
                var byteMacAddress = new byte[]
                {
                    Convert.ToByte(parsedMacAddress.Groups[1].Value, 16),
                    Convert.ToByte(parsedMacAddress.Groups[2].Value, 16),
                    Convert.ToByte(parsedMacAddress.Groups[3].Value, 16),
                    Convert.ToByte(parsedMacAddress.Groups[4].Value, 16),
                    Convert.ToByte(parsedMacAddress.Groups[5].Value, 16),
                    Convert.ToByte(parsedMacAddress.Groups[6].Value, 16),
                };
                var magicPacket = new byte[17 * 6];
                for (var i = 0; i < 6; i++)
                {
                    magicPacket[i] = 0xFF;
                }
                for (var i = 0; i < 16; i++)
                {
                    for (var j = 0; j < 6; j++)
                    {
                        magicPacket[6 + (i * 6) + j] = byteMacAddress[j];
                    }
                }
                using var client = new UdpClient();
                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    if (!IPAddress.TryParse(ipAddress, out var ipAddressParsed))
                    {
                        _logger.LogError($"Failed to parse IP address: {ipAddress}");
                        return 1;
                    }
                    client.Connect(ipAddressParsed, 7);
                }
                else
                {
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
                    client.Connect(IPAddress.Broadcast, 7);
                }

                _logger.LogInformation("Sending Wake-on-LAN magic packets for 10 seconds...");
                for (var i = 0; i < 100; i++)
                {
                    await client.SendAsync(magicPacket, context.GetCancellationToken()).ConfigureAwait(false);
                    await Task.Delay(100, context.GetCancellationToken()).ConfigureAwait(false);
                }
                _logger.LogInformation("Wake-on-LAN magic packet sending complete.");
                return 0;
            }
        }
    }
}
