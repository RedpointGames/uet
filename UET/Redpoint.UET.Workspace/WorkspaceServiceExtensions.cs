namespace Redpoint.UET.Workspace
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.GrpcPipes;
    using Redpoint.Reservation;
    using Redpoint.UET.Workspace.Credential;
    using Redpoint.UET.Workspace.ParallelCopy;
    using Redpoint.UET.Workspace.PhysicalGit;
    using Redpoint.UET.Workspace.Reservation;
    using static Uefs.UEFS;

    public static class WorkspaceServiceExtensions
    {
        public static void AddUETWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<IPhysicalGitCheckout, DefaultPhysicalGitCheckout>();
            services.AddSingleton<IParallelCopy, DefaultParallelCopy>();
            services.AddSingleton<ICredentialManager, DefaultCredentialManager>();
            services.AddSingleton<IPhysicalWorkspaceProvider, PhysicalWorkspaceProvider>();
            services.AddSingleton<IVirtualWorkspaceProvider, VirtualWorkspaceProvider>();
            services.AddSingleton<IDynamicWorkspaceProvider, DynamicWorkspaceProvider>();
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IGrpcPipeFactory>();
                return factory.CreateClient(
                    "UEFS",
                    channel => new UEFSClient(channel));
            });
            services.AddSingleton<IReservationManagerForUet>(sp =>
            {
                var factory = sp.GetRequiredService<IReservationManagerFactory>();
                string rootPath;
                if (OperatingSystem.IsWindows())
                {
                    rootPath = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    rootPath = "/Users/Shared/.ues";
                }
                else
                {
                    rootPath = "/tmp/.ues";
                }
                return new DefaultReservationManagerForUET(factory.CreateReservationManager(rootPath));
            });
        }
    }
}