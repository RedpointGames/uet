using Fsp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using Redpoint.Windows.HostNetworkingService;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace UET.Commands.Cluster
{
    [SupportedOSPlatform("windows")]
    internal sealed class ClusterGetHnsEndpointCommand
    {
        internal sealed class Options
        {
            public Option<string> NetworkName = new Option<string>("--network-name", "The HNS network name.");
            public Option<string> EndpointName = new Option<string>("--endpoint-name", "The HNS endpoint name.");
        }

        public static Command CreateClusterGetHnsEndpointCommand()
        {
            var options = new Options();
            var command = new Command(
                "get-hns-endpoint",
                "Outputs the HNS endpoint for the given HNS network, or exits with an exit code 1.");
            command.IsHidden = true;
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterGetHnsEndpointCommandInstance>(options, services =>
            {
                services.AddSingleton(_ =>
                {
                    return IHnsApi.GetInstance();
                });
            });
            return command;
        }

        private sealed class ClusterGetHnsEndpointCommandInstance : ICommandInstance
        {
            private readonly IHnsApi _hnsService;
            private readonly Options _options;

            public ClusterGetHnsEndpointCommandInstance(
                IHnsApi hnsService,
                Options options)
            {
                _hnsService = hnsService;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var networkName = context.ParseResult.GetValueForOption(_options.NetworkName);
                var endpointName = context.ParseResult.GetValueForOption(_options.EndpointName);

                var newHnsNetwork = _hnsService.GetHnsNetworks().FirstOrDefault(x => x.Name == networkName);
                if (newHnsNetwork == null)
                {
                    return Task.FromResult(1);
                }

                var hnsNetworkAddressPrefix = newHnsNetwork.Subnets[0].AddressPrefix!;

                var newHnsEndpoint = _hnsService.GetHnsEndpoints().FirstOrDefault(x => x.Name == endpointName);
                if (newHnsEndpoint == null)
                {
                    return Task.FromResult(1);
                }

                var sourceVip = newHnsEndpoint.IPAddress!;

                Console.WriteLine(sourceVip);
                return Task.FromResult(0);
            }
        }
    }
}
