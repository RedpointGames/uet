namespace Redpoint.UET.Workspace
{
    using Microsoft.Extensions.DependencyInjection;

    public static class WorkspaceServiceExtensions
    {
        public static void AddUETWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<IWorkspaceProvider, DefaultWorkspaceProvider>();
        }
    }
}