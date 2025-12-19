namespace UET.Commands.Internal.InspectSnmp
{
    using Lextm.SharpSnmpLib;
    using Lextm.SharpSnmpLib.Messaging;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Pdu.Serveredge;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Net;
    using System.Threading.Tasks;

    internal class InspectSnmpCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<InspectSnmpCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("inspect-snmp");
                })
            .Build();

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

        private sealed class InspectSnmpCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<InspectSnmpCommandInstance> _logger;

            public InspectSnmpCommandInstance(
                Options options,
                ILogger<InspectSnmpCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var address = context.ParseResult.GetValueForOption(_options.Address);
                var community = context.ParseResult.GetValueForOption(_options.Community);

                /*
                var oids = new List<string>
                {
                    "1.3.6.1.2.1.1.2",
                    "1.3.6.1.2.1.1.3",
                    "1.3.6.1.2.1.1.4",
                    "1.3.6.1.2.1.1.5",
                    "1.3.6.1.2.1.1.3",
                    "1.3.6.1.2.1.1.6",
                    "1.3.6.1.2.1.1.2.0",
                    "1.3.6.1.2.1.1.3.0",
                    "1.3.6.1.2.1.1.4.0",
                    "1.3.6.1.2.1.1.5.0",
                    "1.3.6.1.2.1.1.3.0",
                    "1.3.6.1.2.1.1.6.0",
                    "1.3.6.1.4.1.3808.1.1.3.0",
                    "1.3.6.1.4.1.3808.1.1.1.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.2.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.2.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.3.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.4.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.5.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.6.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.7.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.8.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.8.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.9.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.10.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.11.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.12.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.13.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.14.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.15.0",
                    "1.3.6.1.4.1.3808.1.1.3.1.16.0",
                    "1.3.6.1.4.1.3808.1.1.4.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.5.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.6.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.7.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.8.1.1.0",

                    "1.3.6.1.4.1.3808.1.1.3.2.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.1",
                    "1.3.6.1.4.1.3808.1.1.3.2.1.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.2.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.3.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.4.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.5.0",
                    "1.3.6.1.4.1.3808.1.1.3.2.6.0",

                    "1.3.6.1.4.1.3808.1.1.3.3.3.0",
                    "1.3.6.1.4.1.3808.1.1.3.3.4.0",
                    "1.3.6.1.4.1.3808.1.1.3.3.5.0",

                    "1.3.6.1.4.1.3808.1.1.3.4.1.1.0",
                    "1.3.6.1.4.1.3808.1.1.3.4.1.2.0",
                };
                foreach (var oid in oids)
                {
                    IList<Variable> v = new List<Variable>
                    {
                        new Variable(new ObjectIdentifier(oid))
                    };
                    try
                    {
                        v = await Messenger.GetAsync(
                            VersionCode.V1,
                            new IPEndPoint(IPAddress.Parse(address!), 161),
                            new OctetString(community!),
                            v,
                            context.GetCancellationToken()).ConfigureAwait(false);
                        _logger.LogInformation($"{oid} = {v[0].Data}");
                    }
                    catch (ErrorException ex)
                    {
                        _logger.LogError($"{oid} = {ex.DetailsPublic}");
                    }
                }
                */

                var walkOids = new List<string>
                {
                    "1.3.6.1.4.1.3808.1.1"
                };
                foreach (var oid in walkOids)
                {
                    var variables = new List<Variable>();
                    await Messenger.WalkAsync(
                        VersionCode.V1,
                        new IPEndPoint(IPAddress.Parse(address!), 161),
                        new OctetString(community!),
                        new ObjectIdentifier(oid),
                        variables,
                        WalkMode.WithinSubtree,
                        context.GetCancellationToken()).ConfigureAwait(false);
                    foreach (var variable in variables)
                    {
                        Console.WriteLine($"{variable.Id} = ({variable.Data.TypeCode}) {variable.Data}");
                    }
                }

                return 0;
            }
        }
    }
}
