namespace Redpoint.UET.Workspace
{
    using GrpcDotNetNamedPipes;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.Workspace.Credential;
    using Redpoint.UET.Workspace.Reservation;
    using static Uefs.UEFS;

    public static class WorkspaceServiceExtensions
    {
        public static void AddUETWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<IReservationManager, DefaultReservationManager>();
            services.AddSingleton<ICredentialManager, DefaultCredentialManager>();
            services.AddSingleton<IWorkspaceProvider, DefaultWorkspaceProvider>();
            services.AddSingleton(sp =>
            {
                var channel = new NamedPipeChannel(".", "UEFS");
                return new UEFSClient(channel);
            });
        }
    }
}