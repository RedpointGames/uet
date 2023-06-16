namespace Redpoint.ProgressMonitor.Implementations
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    internal class DefaultMonitorFactory : IMonitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultMonitorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IByteBasedMonitor CreateByteBasedMonitor()
        {
            return new DefaultByteBasedMonitor(
                _serviceProvider.GetRequiredService<IUtilities>());
        }

        public IGitFetchBasedMonitor CreateGitFetchBasedMonitor()
        {
            return new DefaultGitFetchBasedMonitor(
                _serviceProvider.GetRequiredService<IUtilities>());
        }

        public ITaskBasedMonitor CreateTaskBasedMonitor()
        {
            return new DefaultTaskBasedMonitor();
        }
    }
}
