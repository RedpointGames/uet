namespace Redpoint.OpenGE.Component.Worker
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Component.Worker.DriveMapping;
    using Redpoint.OpenGE.Component.Worker.PchPortability;
    using Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors;
    using Redpoint.OpenGE.Protocol;

    public static class WorkerServiceExtensions
    {
        public static void AddOpenGEComponentWorker(this IServiceCollection services)
        {
            services.AddSingleton<IToolManager, DefaultToolManager>();
            services.AddSingleton<IBlobManager, DefaultBlobManager>();
            services.AddSingleton<IExecutionManager, DefaultExecutionManager>();
            services.AddSingleton<IWorkerComponentFactory, DefaultWorkerComponentFactory>();
            if (OperatingSystem.IsWindowsVersionAtLeast(5, 0))
            {
                services.AddSingleton<IDirectoryDriveMapping, WindowsDirectoryDriveMapping>();
            }
            else
            {
                services.AddSingleton<IDirectoryDriveMapping, UnixDirectoryDriveMapping>();
            }
            services.AddSingleton<IPchPortability, DefaultPchPortability>();

            // @note: These also need to be injected into DefaultExecutionManager in
            // order for descriptors to actually execute.
            services.AddSingleton<ITaskDescriptorExecutor<LocalTaskDescriptor>, LocalTaskDescriptorExecutor>();
            services.AddSingleton<ITaskDescriptorExecutor<CopyTaskDescriptor>, CopyTaskDescriptorExecutor>();
            services.AddSingleton<ITaskDescriptorExecutor<RemoteTaskDescriptor>, RemoteTaskDescriptorExecutor>();
        }
    }
}
