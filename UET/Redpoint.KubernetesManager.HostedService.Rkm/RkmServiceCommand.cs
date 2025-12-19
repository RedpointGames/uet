namespace UET.Commands.Internal.Rkm
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager;
    using Redpoint.KubernetesManager.HostedService;
    using System.CommandLine;

    public sealed class RkmServiceCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<RunRkmServiceCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("rkm-service");
                    return command;
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm";
                        });
                    }
                    services.AddKubernetesManager(true);
                    services.AddRkmHostedServiceEnvironment("rkm");
                    services.AddHostedService<RKMWorker>();
                })
            .Build();

        internal sealed class Options
        {
        }
    }
}
