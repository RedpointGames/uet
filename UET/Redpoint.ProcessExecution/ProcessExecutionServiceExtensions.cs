namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ProcessExecutionServiceExtensions
    {
        public static void AddProcessExecution(this IServiceCollection services)
        {
            services.AddSingleton<IProcessExecutor, DefaultProcessExecutor>();
            services.AddSingleton<IScriptExecutor, DefaultScriptExecutor>();
        }
    }
}
