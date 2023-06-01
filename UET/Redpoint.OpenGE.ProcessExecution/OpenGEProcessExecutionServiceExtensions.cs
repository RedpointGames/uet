namespace Redpoint.OpenGE.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;

    public static class OpenGEProcessExecutionServiceExtensions
    {
        public static void AddOpenGEProcessExecution(this IServiceCollection services)
        {
            services.AddSingleton<IProcessWithOpenGEExecutor, DefaultProcessWithOpenGEExecutor>();
        }
    }
}
