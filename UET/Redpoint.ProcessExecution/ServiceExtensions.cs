namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceExtensions
    {
        public static void AddExecutors(this IServiceCollection services)
        {
            services.AddSingleton<IProcessExecutor, DefaultProcessExecutor>();
            services.AddSingleton<IScriptExecutor, DefaultScriptExecutor>();
        }
    }
}
