namespace Redpoint.UET.Workspace
{
    using GrpcDotNetNamedPipes;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.UET.Workspace.Credential;
    using static Uefs.UEFS;

    public static class WorkspaceServiceExtensions
    {
        public static void AddUETWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<ICredentialManager, DefaultCredentialManager>();
            services.AddSingleton<IWorkspaceProvider, DefaultWorkspaceProvider>();
            services.AddSingleton(sp =>
            {
                var channel = new NamedPipeChannel(".", "UEFS");
                return new UEFSClient(channel);
            });
            services.AddSingleton<IReservationManagerForUET>(sp =>
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