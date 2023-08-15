namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution.Windows;

    public static class ProcessExecutionServiceExtensions
    {
        public static void AddProcessExecution(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                services.AddSingleton<IProcessExecutor, WindowsProcessExecutor>();
            }
            else
            {
                services.AddSingleton<IProcessExecutor, DefaultProcessExecutor>();
            }
            services.AddSingleton<IScriptExecutor, DefaultScriptExecutor>();
        }
    }
}
