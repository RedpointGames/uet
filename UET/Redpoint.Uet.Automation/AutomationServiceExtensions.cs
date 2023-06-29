namespace Redpoint.Uet.Automation
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Automation.Runner;
    using Redpoint.Uet.Automation.SystemResources;
    using Redpoint.Uet.Automation.TestLogger;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using Redpoint.Uet.Automation.Worker.Local;

    public static class AutomationServiceExtensions
    {
        public static void AddUETAutomation(this IServiceCollection services)
        {
            services.AddSingleton<ITestLoggerFactory, DefaultTestLoggerFactory>();
            services.AddSingleton<ITestNotificationFactory, DefaultTestNotificationFactory>();
            services.AddSingleton<ITestReporterFactory, DefaultTestReporterFactory>();
            services.AddSingleton<IWorkerPoolFactory, LocalWorkerPoolFactory>();
            services.AddSingleton<IAutomationRunnerFactory, DefaultAutomationRunnerFactory>();
            services.AddSingleton<IAutomationLogForwarder, GrpcTestLoggerServerLifecycle>();
            services.AddSingleton<IProcessExecutorHook, AutomationLogForwarderProcessExecutorHook>();
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<ISystemResources, WindowsSystemResources>();
            }
            else
            {
                services.AddSingleton<ISystemResources, NullSystemResources>();
            }
        }
    }
}
