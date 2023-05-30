namespace Redpoint.ProgressMonitor
{
    internal class DefaultMonitorFactory : IMonitorFactory
    {
        public IByteBasedMonitor CreateByteBasedMonitor()
        {
            return new DefaultByteBasedMonitor();
        }
    }
}
