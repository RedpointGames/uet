namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution.Windows;

    /// <summary>
    /// Registers the <see cref="IProcessExecutor"/> and <see cref="IScriptExecutor"/> services with dependency injection.
    /// </summary>
    public static class ProcessExecutionServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IProcessExecutor"/> and <see cref="IScriptExecutor"/> services with dependency injection.
        /// </summary>
        public static void AddProcessExecution(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                services.AddSingleton<DefaultProcessExecutor, DefaultProcessExecutor>();
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
