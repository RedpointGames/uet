namespace Redpoint.UET.Automation
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Automation.Runner;
    using Redpoint.UET.Automation.SystemResources;
    using Redpoint.UET.Automation.TestLogger;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestNotification.Io;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using Redpoint.UET.Automation.Worker.Local;

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
