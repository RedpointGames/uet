namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace.Credential;
    using Redpoint.Uet.Workspace.ParallelCopy;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using Redpoint.Uet.Workspace.Reservation;

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