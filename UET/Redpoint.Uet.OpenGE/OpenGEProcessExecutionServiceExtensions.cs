namespace Redpoint.Uet.OpenGE
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution;

    public static class OpenGEProcessExecutionServiceExtensions
    {
        public static void AddOpenGEProcessExecution(this IServiceCollection services)
        {
            services.AddSingleton<IProcessExecutorHook, OpenGEProcessExecutorHook>();
        }
    }
}
