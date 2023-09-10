namespace Redpoint.Tasks
{
    using Microsoft.Extensions.DependencyInjection;

    public static class TasksServiceExtensions
    {
        public static void AddTasks(this IServiceCollection services)
        {
            services.AddSingleton<ITaskScheduler, DefaultTaskScheduler>();
        }
    }
}
