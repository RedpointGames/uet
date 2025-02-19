namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Workspace.ParallelCopy;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using Redpoint.Uet.Workspace.Reservation;
    using Redpoint.Uet.Workspace.Storage;

    public static class WorkspaceServiceExtensions
    {
        public static void AddUetWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<IPhysicalGitCheckout, DefaultPhysicalGitCheckout>();
            services.AddSingleton<IParallelCopy, DefaultParallelCopy>();
            services.AddSingleton<IWorkspaceProvider, DefaultWorkspaceProvider>();
            services.AddSingleton<IReservationManagerForUet>(sp =>
            {
                var factory = sp.GetRequiredService<IReservationManagerFactory>();
                var rootPath = UetPaths.UetBuildReservationPath;
                return new DefaultReservationManagerForUet(factory.CreateReservationManager(rootPath), rootPath);
            });
            services.AddSingleton<IStorageManagement, DefaultStorageManagement>();
            services.AddSingleton<IWorkspaceReservationParameterGenerator, DefaultWorkspaceReservationParameterGenerator>();
        }
    }
}